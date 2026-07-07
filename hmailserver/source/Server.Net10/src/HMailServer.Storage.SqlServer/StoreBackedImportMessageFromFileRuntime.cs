using System.Globalization;
using System.Text;
using HMailServer.Core.Abstractions;
using MimeKit;

namespace HMailServer.Storage.SqlServer;

public sealed class StoreBackedImportMessageFromFileRuntime : IImportMessageFromFileRuntime
{
    private const string PublicFolderDiskName = "#Public";
    private const string InboxFolderName = "Inbox";

    private readonly IImportMessageFromFileStore _store;
    private readonly ISmtpRecipientValidator _recipientValidator;
    private readonly IDeliveryQueueWakeSignal _wakeSignal;
    private readonly TimeProvider _timeProvider;
    private readonly string _dataDirectory;
    private readonly string _hierarchyDelimiter;
    private readonly string _publicFolderName;

    public StoreBackedImportMessageFromFileRuntime(
        IImportMessageFromFileStore store,
        ISmtpRecipientValidator recipientValidator,
        IDeliveryQueueWakeSignal wakeSignal,
        string dataDirectory,
        TimeProvider? timeProvider = null,
        SqlServerImapMailboxStoreOptions? mailboxOptions = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(recipientValidator);
        ArgumentNullException.ThrowIfNull(wakeSignal);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _store = store;
        _recipientValidator = recipientValidator;
        _wakeSignal = wakeSignal;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _dataDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
        _hierarchyDelimiter = mailboxOptions?.HierarchyDelimiter ?? ".";
        _publicFolderName = mailboxOptions?.PublicFolderName ?? PublicFolderDiskName;
    }

    public async ValueTask<bool> ImportMessageFromFileAsync(
        string fileName,
        int accountId,
        CancellationToken cancellationToken) =>
        await ImportMessageCoreAsync(
            fileName,
            accountId,
            imapFolder: null,
            cancellationToken).ConfigureAwait(false);

    public async ValueTask<bool> ImportMessageFromFileToImapFolderAsync(
        string fileName,
        int accountId,
        string imapFolder,
        CancellationToken cancellationToken) =>
        await ImportMessageCoreAsync(
            fileName,
            accountId,
            imapFolder,
            cancellationToken).ConfigureAwait(false);

    private async ValueTask<bool> ImportMessageCoreAsync(
        string fileName,
        int accountId,
        string? imapFolder,
        CancellationToken cancellationToken)
    {
        try
        {
            if (accountId < 0 || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(fileName);
            if (!IsUnderDataDirectory(fullPath) || !File.Exists(fullPath))
            {
                return false;
            }

            var hasPartialFileName = TryGetPartialFileName(fullPath, out var partialFileName);
            var existing = await _store.FindExistingMessageAsync(
                hasPartialFileName ? partialFileName : null,
                fullPath,
                cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                if (existing.IsPartialFileName)
                {
                    return true;
                }

                if (!hasPartialFileName &&
                    !TryMoveToLegacyPath(fullPath, accountId, out fullPath, out partialFileName))
                {
                    return false;
                }

                return await _store.UpdateMessageFileNameAsync(
                    existing.MessageId,
                    partialFileName,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!HasExpectedPlacement(fullPath, accountId))
            {
                return false;
            }

            if (!hasPartialFileName &&
                !TryMoveToLegacyPath(fullPath, accountId, out fullPath, out partialFileName))
            {
                return false;
            }

            var rawMessage = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (rawMessage.Length == 0)
            {
                return false;
            }

            MimeMessage message;
            using (var stream = new MemoryStream(rawMessage, writable: false))
            {
                message = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            var fromAddress = message.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty;
            var createdUtc = ResolveInternalDate(message);
            if (accountId > 0)
            {
                var folderId = await ResolveAccountFolderIdAsync(
                    accountId,
                    imapFolder,
                    cancellationToken).ConfigureAwait(false);
                if (!folderId.HasValue)
                {
                    return false;
                }

                await _store.ImportDeliveredMessageAsync(
                    new ImportedDeliveredMessage(
                        accountId,
                        folderId.Value,
                        partialFileName,
                        fromAddress,
                        rawMessage.LongLength,
                        createdUtc),
                    cancellationToken).ConfigureAwait(false);
                return true;
            }

            var recipients = await ResolveLocalRecipientsAsync(
                message,
                fromAddress,
                cancellationToken).ConfigureAwait(false);
            if (recipients.Count == 0)
            {
                return false;
            }

            await _store.ImportQueuedMessageAsync(
                new ImportedQueuedMessage(
                    partialFileName,
                    fromAddress,
                    rawMessage.LongLength,
                    createdUtc,
                    recipients),
                cancellationToken).ConfigureAwait(false);
            TrySignalDelivery();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async ValueTask<IReadOnlyList<SmtpResolvedRecipient>> ResolveLocalRecipientsAsync(
        MimeMessage message,
        string fromAddress,
        CancellationToken cancellationToken)
    {
        var recipients = new List<SmtpResolvedRecipient>();
        foreach (var mailbox in message.To.Mailboxes.Concat(message.Cc.Mailboxes))
        {
            var result = await _recipientValidator.ValidateAsync(
                new SmtpRecipientValidationRequest(
                    fromAddress,
                    mailbox.Address,
                    SenderAuthenticated: true,
                    BypassDistributionListAuthorization: true),
                cancellationToken).ConfigureAwait(false);
            if (!result.Accepted)
            {
                continue;
            }

            foreach (var recipient in result.Recipients.Where(static candidate =>
                         candidate.LocalAccountId > 0))
            {
                if (recipients.Any(existing =>
                        existing.Address.Equals(recipient.Address, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                recipients.Add(recipient with { IsLocal = true, Route = null });
            }
        }

        return recipients;
    }

    private DateTimeOffset ResolveInternalDate(MimeMessage message)
    {
        foreach (var header in message.Headers.Where(static header =>
                     header.Field.Equals("Received", StringComparison.OrdinalIgnoreCase)))
        {
            if (TryParseReceivedHeaderDate(header.Value, out var receivedDate) &&
                IsLegacyDate(receivedDate))
            {
                return receivedDate.ToUniversalTime();
            }
        }

        return IsLegacyDate(message.Date)
            ? message.Date.ToUniversalTime()
            : _timeProvider.GetUtcNow();
    }

    private async ValueTask<long?> ResolveAccountFolderIdAsync(
        int accountId,
        string? imapFolder,
        CancellationToken cancellationToken)
    {
        var folderPath = ResolveFolderPath(imapFolder);
        if (folderPath is null)
        {
            return null;
        }

        if (folderPath.IsPublicFolder)
        {
            return null;
        }

        var encodedSegments = folderPath.Segments
            .Select(EncodeMailboxSegment)
            .ToArray();
        return await _store.FindAccountFolderAsync(
            accountId,
            encodedSegments,
            cancellationToken).ConfigureAwait(false);
    }

    private SqlServerImapMailboxPath? ResolveFolderPath(string? imapFolder)
    {
        if (string.IsNullOrEmpty(imapFolder))
        {
            return new SqlServerImapMailboxPath(false, [InboxFolderName]);
        }

        var cleaned = CleanImapFolderPath(imapFolder);
        if (cleaned.Length == 0)
        {
            return null;
        }

        return SqlServerImapMailboxPath.Parse(
            cleaned,
            _hierarchyDelimiter,
            _publicFolderName);
    }

    private string CleanImapFolderPath(string value)
    {
        var localNow = _timeProvider.GetLocalNow();
        var year = localNow.Year.ToString("0000", CultureInfo.InvariantCulture);
        var month = localNow.Month.ToString("00", CultureInfo.InvariantCulture);
        var day = localNow.Day.ToString("00", CultureInfo.InvariantCulture);

        var cleaned = value
            .Replace("%YEAR%", year, StringComparison.OrdinalIgnoreCase)
            .Replace("%MONTH%", month, StringComparison.OrdinalIgnoreCase)
            .Replace("%DAY%", day, StringComparison.OrdinalIgnoreCase);
        if (cleaned.StartsWith(_hierarchyDelimiter, StringComparison.Ordinal))
        {
            cleaned = cleaned[_hierarchyDelimiter.Length..];
        }

        return cleaned;
    }

    private bool TryGetPartialFileName(string fullPath, out string partialFileName)
    {
        partialFileName = string.Empty;
        var relativePath = Path.GetRelativePath(_dataDirectory, fullPath);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 1)
        {
            partialFileName = segments[0];
            return true;
        }

        var bucketIndex = segments[0].Equals(PublicFolderDiskName, StringComparison.OrdinalIgnoreCase)
            ? 1
            : 2;
        if (segments.Length != bucketIndex + 2 ||
            segments[bucketIndex].Length != 2 ||
            !IsMatchingBucket(segments[bucketIndex], segments[bucketIndex + 1]))
        {
            return false;
        }

        partialFileName = segments[bucketIndex + 1];
        return true;
    }

    private bool TryMoveToLegacyPath(
        string sourcePath,
        int accountId,
        out string destinationPath,
        out string partialFileName)
    {
        destinationPath = sourcePath;
        partialFileName = string.Empty;
        var relativePath = Path.GetRelativePath(_dataDirectory, sourcePath);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        string rootDirectory;
        if (accountId > 0)
        {
            if (segments.Length < 3)
            {
                return false;
            }

            rootDirectory = Path.Combine(_dataDirectory, segments[0], segments[1]);
        }
        else if (segments.Length == 1)
        {
            rootDirectory = _dataDirectory;
        }
        else
        {
            return false;
        }

        partialFileName = Guid.NewGuid().ToString("B").ToUpperInvariant() + ".eml";
        var bucket = partialFileName.Substring(1, 2);
        var destinationDirectory = Path.Combine(rootDirectory, bucket);
        Directory.CreateDirectory(destinationDirectory);
        destinationPath = Path.Combine(destinationDirectory, partialFileName);
        File.Move(sourcePath, destinationPath);
        return true;
    }

    private bool HasExpectedPlacement(string fullPath, int accountId)
    {
        var relativePath = Path.GetRelativePath(_dataDirectory, fullPath);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return accountId > 0
            ? segments.Length >= 3
            : segments.Length == 1;
    }

    private bool IsUnderDataDirectory(string fullPath) =>
        fullPath.StartsWith(
            _dataDirectory + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsMatchingBucket(string bucket, string fileName)
    {
        if (fileName.Length >= 3 && fileName[0] == '{' &&
            bucket.Equals(fileName.Substring(1, 2), StringComparison.Ordinal))
        {
            return true;
        }

        return fileName.Length >= 2 &&
               bucket.Equals(fileName[..2], StringComparison.Ordinal);
    }

    private static bool TryParseReceivedHeaderDate(
        string headerValue,
        out DateTimeOffset value)
    {
        var separator = headerValue.LastIndexOf(';');
        var dateText = separator >= 0 && separator < headerValue.Length - 1
            ? headerValue[(separator + 1)..]
            : headerValue;
        return DateTimeOffset.TryParse(
            dateText.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out value);
    }

    private static bool IsLegacyDate(DateTimeOffset value) =>
        value.Year > 1980 && value.Year < 2040;

    private static string EncodeMailboxSegment(string value)
    {
        var output = new System.Text.StringBuilder(value.Length);
        var position = 0;
        while (position < value.Length)
        {
            var current = value[position];
            if (!IsMailboxEncodingSpecial(current))
            {
                output.Append(current);
                if (current == '&')
                {
                    output.Append('-');
                }

                position++;
                continue;
            }

            var start = position;
            while (position < value.Length && IsMailboxEncodingSpecial(value[position]))
            {
                position++;
            }

            var bytes = BigEndianUnicode.GetBytes(value[start..position]);
            output.Append('&');
            output.Append(Convert.ToBase64String(bytes).TrimEnd('='));
            output.Append('-');
        }

        return output.ToString();
    }

    private static bool IsMailboxEncodingSpecial(char value) => value < 32 || value >= 127;

    private void TrySignalDelivery()
    {
        try
        {
            _wakeSignal.Signal();
        }
        catch (Exception)
        {
            // The message is durable; the delivery worker's idle poll will find it.
        }
    }

    private static readonly Encoding BigEndianUnicode =
        new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);
}
