using System.Globalization;
using HMailServer.Core.Abstractions;
using MimeKit;

namespace HMailServer.Protocols.Pop3;

public sealed class ExternalFetchProcessor
{
    private readonly IExternalFetchAccountStore _accountStore;
    private readonly IExternalFetchSessionFactory _sessionFactory;
    private readonly ISmtpMessageReceiver _messageReceiver;
    private readonly IExternalAccountDownloadScriptExecutor? _scriptExecutor;
    private readonly IMessageAntivirusScanner? _antivirusScanner;
    private readonly ISmtpRecipientValidator? _recipientValidator;
    private readonly TimeProvider _timeProvider;

    public ExternalFetchProcessor(
        IExternalFetchAccountStore accountStore,
        IExternalFetchSessionFactory sessionFactory,
        ISmtpMessageReceiver messageReceiver,
        IExternalAccountDownloadScriptExecutor? scriptExecutor = null,
        IMessageAntivirusScanner? antivirusScanner = null,
        ISmtpRecipientValidator? recipientValidator = null,
        TimeProvider? timeProvider = null)
    {
        _accountStore = accountStore;
        _sessionFactory = sessionFactory;
        _messageReceiver = messageReceiver;
        _scriptExecutor = scriptExecutor;
        _antivirusScanner = antivirusScanner;
        _recipientValidator = recipientValidator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<ExternalFetchProcessorResult> RunBatchAsync(
        ExternalFetchProcessorOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxMessagesPerAccount);

        var deferredInactiveAccounts = await _accountStore
            .DeferInactiveAccountsAsync(cancellationToken)
            .ConfigureAwait(false);
        var accountsLeased = 0;
        var accountsCompleted = 0;
        var accountsFailed = 0;
        var messagesDownloaded = 0;
        var messagesAccepted = 0;
        var remoteMessagesDeleted = 0;
        var knownUidsAdded = 0;
        var knownUidsDeleted = 0;

        await foreach (var account in _accountStore.LeaseReadyAccountsAsync(options.BatchSize, cancellationToken).ConfigureAwait(false))
        {
            accountsLeased++;
            try
            {
                var accountResult = await ProcessAccountAsync(account, options, cancellationToken).ConfigureAwait(false);
                messagesDownloaded += accountResult.MessagesDownloaded;
                messagesAccepted += accountResult.MessagesAccepted;
                remoteMessagesDeleted += accountResult.RemoteMessagesDeleted;
                knownUidsAdded += accountResult.KnownUidsAdded;
                knownUidsDeleted += accountResult.KnownUidsDeleted;

                if (await _accountStore.CompleteAsync(account.FetchAccountId, cancellationToken).ConfigureAwait(false))
                {
                    accountsCompleted++;
                }
                else
                {
                    accountsFailed++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                accountsFailed++;
                await ReleaseAccountSafelyAsync(account.FetchAccountId, cancellationToken).ConfigureAwait(false);
            }
        }

        return new ExternalFetchProcessorResult(
            deferredInactiveAccounts,
            accountsLeased,
            accountsCompleted,
            accountsFailed,
            messagesDownloaded,
            messagesAccepted,
            remoteMessagesDeleted,
            knownUidsAdded,
            knownUidsDeleted);
    }

    private async ValueTask<AccountFetchResult> ProcessAccountAsync(
        ExternalFetchAccountLease account,
        ExternalFetchProcessorOptions options,
        CancellationToken cancellationToken)
    {
        var knownUids = await _accountStore
            .LoadKnownUidsAsync(account.FetchAccountId, cancellationToken)
            .ConfigureAwait(false);
        var knownByUid = knownUids.ToDictionary(static uid => uid.Value, StringComparer.Ordinal);

        await using var session = await _sessionFactory.ConnectAsync(account, cancellationToken).ConfigureAwait(false);
        var remoteMessages = await session.ListMessagesAsync(cancellationToken).ConfigureAwait(false);
        var remoteUidValues = remoteMessages
            .Select(static message => message.Uid)
            .ToHashSet(StringComparer.Ordinal);

        var knownUidsDeleted = await DeleteMissingKnownUidsAsync(
            account,
            knownUids,
            remoteUidValues,
            cancellationToken).ConfigureAwait(false);

        var remoteMessagesDeleted = 0;
        foreach (var remoteMessage in remoteMessages)
        {
            if (!knownByUid.TryGetValue(remoteMessage.Uid, out var knownUid))
            {
                continue;
            }

            var scriptResult = RunExternalAccountDownloadScript(account, remoteMessage.Uid, messageData: null, cancellationToken);
            var daysToKeep = ResolveDaysToKeep(account.DaysToKeep, scriptResult);
            if (!ShouldDeleteExistingRemoteMessage(daysToKeep, knownUid.CreatedAt, _timeProvider.GetUtcNow()))
            {
                continue;
            }

            await session.DeleteMessageAsync(remoteMessage, cancellationToken).ConfigureAwait(false);
            if (await _accountStore.DeleteKnownUidAsync(knownUid.Id, cancellationToken).ConfigureAwait(false))
            {
                knownUidsDeleted++;
            }

            remoteMessagesDeleted++;
        }

        var messagesDownloaded = 0;
        var messagesAccepted = 0;
        var knownUidsAdded = 0;
        foreach (var remoteMessage in remoteMessages)
        {
            if (knownByUid.ContainsKey(remoteMessage.Uid))
            {
                continue;
            }

            if (messagesDownloaded >= options.MaxMessagesPerAccount)
            {
                break;
            }

            var messageData = await session.DownloadMessageAsync(remoteMessage, cancellationToken).ConfigureAwait(false);
            if (messageData.Length == 0)
            {
                throw new InvalidOperationException("External POP3 message was empty.");
            }

            messagesDownloaded++;
            var scriptResult = RunExternalAccountDownloadScript(account, remoteMessage.Uid, messageData, cancellationToken);
            var acceptedMessageData = scriptResult.MessageData ?? messageData;
            var antivirusResult = await RunAntivirusScanAsync(account, acceptedMessageData, cancellationToken).ConfigureAwait(false);
            if (antivirusResult.IsInfected)
            {
                var infectedRetention = await ApplyNewMessageRetentionAsync(
                    account,
                    session,
                    remoteMessage,
                    scriptResult,
                    cancellationToken).ConfigureAwait(false);
                remoteMessagesDeleted += infectedRetention.RemoteMessagesDeleted;
                knownUidsAdded += infectedRetention.KnownUidsAdded;
                continue;
            }

            var mimeMessage = TryLoadMimeMessage(acceptedMessageData);
            var receiveRequest = await CreateReceiveRequestAsync(
                account,
                remoteMessage,
                acceptedMessageData,
                mimeMessage,
                cancellationToken).ConfigureAwait(false);
            var receiveResult = await _messageReceiver
                .ReceiveAsync(receiveRequest, cancellationToken)
                .ConfigureAwait(false);
            if (!receiveResult.Accepted)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(receiveResult.FailureResponse)
                        ? "External POP3 message receiver rejected the message."
                        : receiveResult.FailureResponse);
            }

            messagesAccepted++;
            var retention = await ApplyNewMessageRetentionAsync(
                account,
                session,
                remoteMessage,
                scriptResult,
                cancellationToken).ConfigureAwait(false);
            remoteMessagesDeleted += retention.RemoteMessagesDeleted;
            knownUidsAdded += retention.KnownUidsAdded;
        }

        return new AccountFetchResult(
            messagesDownloaded,
            messagesAccepted,
            remoteMessagesDeleted,
            knownUidsAdded,
            knownUidsDeleted);
    }

    private async ValueTask<NewMessageRetentionResult> ApplyNewMessageRetentionAsync(
        ExternalFetchAccountLease account,
        IExternalFetchSession session,
        ExternalFetchRemoteMessage remoteMessage,
        ExternalAccountDownloadScriptExecutionResult scriptResult,
        CancellationToken cancellationToken)
    {
        var daysToKeep = ResolveDaysToKeep(account.DaysToKeep, scriptResult);
        if (daysToKeep != -1)
        {
            await _accountStore.AddKnownUidAsync(account.FetchAccountId, remoteMessage.Uid, cancellationToken).ConfigureAwait(false);
            return new NewMessageRetentionResult(RemoteMessagesDeleted: 0, KnownUidsAdded: 1);
        }

        await session.DeleteMessageAsync(remoteMessage, cancellationToken).ConfigureAwait(false);
        return new NewMessageRetentionResult(RemoteMessagesDeleted: 1, KnownUidsAdded: 0);
    }

    private async ValueTask<int> DeleteMissingKnownUidsAsync(
        ExternalFetchAccountLease account,
        IReadOnlyList<ExternalFetchKnownUid> knownUids,
        HashSet<string> remoteUidValues,
        CancellationToken cancellationToken)
    {
        var deleted = 0;
        foreach (var knownUid in knownUids)
        {
            if (remoteUidValues.Contains(knownUid.Value))
            {
                continue;
            }

            if (await _accountStore.DeleteKnownUidAsync(knownUid.Id, cancellationToken).ConfigureAwait(false))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private ExternalAccountDownloadScriptExecutionResult RunExternalAccountDownloadScript(
        ExternalFetchAccountLease account,
        string remoteUid,
        byte[]? messageData,
        CancellationToken cancellationToken)
    {
        if (_scriptExecutor is null)
        {
            return ExternalAccountDownloadScriptExecutionResult.Continue(messageData);
        }

        var result = _scriptExecutor.Execute(
            new ExternalAccountDownloadScriptExecutionRequest(account, remoteUid, messageData),
            cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.Error)
                    ? "External account download script execution failed."
                    : result.Error);
        }

        return result;
    }

    private async ValueTask<MessageAntivirusScanResult> RunAntivirusScanAsync(
        ExternalFetchAccountLease account,
        byte[] messageData,
        CancellationToken cancellationToken)
    {
        if (_antivirusScanner is null || !account.UseAntiVirus)
        {
            return MessageAntivirusScanResult.Clean();
        }

        var result = await _antivirusScanner.ScanAsync(messageData, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.Details)
                    ? "External POP3 antivirus scan failed."
                    : result.Details);
        }

        return result;
    }

    private async ValueTask<SmtpReceiveRequest> CreateReceiveRequestAsync(
        ExternalFetchAccountLease account,
        ExternalFetchRemoteMessage remoteMessage,
        byte[] messageData,
        MimeMessage? mimeMessage,
        CancellationToken cancellationToken)
    {
        var receivedUtc = ResolveReceivedUtc(account, mimeMessage, _timeProvider.GetUtcNow());
        var mailFrom = ExtractMailFrom(mimeMessage);
        var recipients = await ResolveRecipientsAsync(account, mimeMessage, mailFrom, cancellationToken).ConfigureAwait(false);
        return new SmtpReceiveRequest(
            HeloHost: account.ServerAddress,
            IsExtendedSmtp: true,
            MailFrom: mailFrom,
            Recipients: recipients,
            DeclaredSize: remoteMessage.Size > 0 ? remoteMessage.Size : messageData.LongLength,
            MessageData: messageData,
            ReceivedUtc: receivedUtc,
            ClientIPAddress: account.ServerAddress,
            ClientPort: account.ServerPort,
            SessionId: 0,
            AuthenticatedUsername: account.Username,
            IsAuthenticated: true,
            IsEncryptedConnection: account.ConnectionSecurity != ExternalFetchConnectionSecurity.None,
            EnableAntivirusScan: account.UseAntiVirus,
            EnableSpamScan: account.UseAntiSpam);
    }

    private static MimeMessage? TryLoadMimeMessage(byte[] messageData)
    {
        try
        {
            using var input = new MemoryStream(messageData, writable: false);
            return MimeMessage.Load(input);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string ExtractMailFrom(MimeMessage? mimeMessage) =>
        mimeMessage?.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty;

    private static DateTimeOffset ResolveReceivedUtc(
        ExternalFetchAccountLease account,
        MimeMessage? mimeMessage,
        DateTimeOffset fallback)
    {
        if (!account.ProcessMimeDate || mimeMessage is null)
        {
            return fallback;
        }

        foreach (var header in mimeMessage.Headers.Where(static header =>
            header.Field.Equals("Received", StringComparison.OrdinalIgnoreCase)))
        {
            if (TryParseReceivedHeaderDate(header.Value, out var receivedDate) &&
                IsLegacyExternalFetchDate(receivedDate))
            {
                return receivedDate.ToUniversalTime();
            }
        }

        if (IsLegacyExternalFetchDate(mimeMessage.Date))
        {
            return mimeMessage.Date.ToUniversalTime();
        }

        return fallback;
    }

    private static bool TryParseReceivedHeaderDate(
        string headerValue,
        out DateTimeOffset value)
    {
        var dateText = headerValue;
        var separator = headerValue.LastIndexOf(';');
        if (separator >= 0 && separator < headerValue.Length - 1)
        {
            dateText = headerValue[(separator + 1)..];
        }

        return DateTimeOffset.TryParse(
            dateText.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out value);
    }

    private static bool IsLegacyExternalFetchDate(DateTimeOffset value)
    {
        var year = value.UtcDateTime.Year;
        return year is >= 1980 and <= 2040;
    }

    private async ValueTask<IReadOnlyList<SmtpResolvedRecipient>> ResolveRecipientsAsync(
        ExternalFetchAccountLease account,
        MimeMessage? mimeMessage,
        string mailFrom,
        CancellationToken cancellationToken)
    {
        var recipients = new List<SmtpResolvedRecipient>();
        var candidateAddresses = ExtractRecipientCandidates(account, mimeMessage);

        foreach (var address in candidateAddresses)
        {
            await AddResolvedRecipientAsync(account, mailFrom, address, recipients, cancellationToken).ConfigureAwait(false);
        }

        if (recipients.Count == 0)
        {
            AddFallbackRecipient(account, recipients);
        }

        return recipients;
    }

    private static IReadOnlyList<string> ExtractRecipientCandidates(
        ExternalFetchAccountLease account,
        MimeMessage? mimeMessage)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (mimeMessage is not null &&
            account.ProcessMimeRecipients &&
            !string.IsNullOrWhiteSpace(account.MimeRecipientHeaders))
        {
            foreach (var headerName in SplitMimeRecipientHeaders(account.MimeRecipientHeaders))
            {
                foreach (var header in mimeMessage.Headers.Where(header =>
                    header.Field.Equals(headerName, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var mailbox in ParseMailboxes(header.Value))
                    {
                        AddCandidate(mailbox.Address, candidates, seen);
                    }
                }
            }

            foreach (var header in mimeMessage.Headers.Where(static header =>
                header.Field.Equals("Received", StringComparison.OrdinalIgnoreCase)))
            {
                if (TryExtractRecipientFromReceivedHeader(header.Value, out var receivedRecipient))
                {
                    AddCandidate(receivedRecipient, candidates, seen);
                }
            }
        }

        return candidates;
    }

    private static IEnumerable<string> SplitMimeRecipientHeaders(string value) =>
        value.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<MailboxAddress> ParseMailboxes(string headerValue)
    {
        InternetAddressList addresses;
        try
        {
            addresses = InternetAddressList.Parse(headerValue);
        }
        catch (ParseException)
        {
            yield break;
        }

        foreach (var mailbox in addresses.Mailboxes)
        {
            yield return mailbox;
        }
    }

    private async ValueTask AddResolvedRecipientAsync(
        ExternalFetchAccountLease account,
        string mailFrom,
        string address,
        List<SmtpResolvedRecipient> recipients,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        if (_recipientValidator is null)
        {
            AddLegacyFallbackResolvedRecipient(account, address, recipients);
            return;
        }

        var result = await _recipientValidator
            .ValidateAsync(
                new SmtpRecipientValidationRequest(mailFrom, address, SenderAuthenticated: true),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Accepted)
        {
            return;
        }

        foreach (var recipient in result.Recipients)
        {
            if (!ShouldKeepResolvedRecipient(account, recipient))
            {
                continue;
            }

            AddResolvedRecipient(recipients, recipient);
        }
    }

    private static void AddLegacyFallbackResolvedRecipient(
        ExternalFetchAccountLease account,
        string address,
        List<SmtpResolvedRecipient> recipients)
    {
        var isAccountAddress = address.Equals(account.AccountAddress, StringComparison.OrdinalIgnoreCase);
        if (!isAccountAddress && !account.EnableRouteRecipients)
        {
            return;
        }

        AddResolvedRecipient(
            recipients,
            new SmtpResolvedRecipient(
                address,
                address,
                isAccountAddress ? account.AccountId : 0,
                isAccountAddress));
    }

    private static bool ShouldKeepResolvedRecipient(
        ExternalFetchAccountLease account,
        SmtpResolvedRecipient recipient) =>
        account.EnableRouteRecipients
            ? recipient.IsLocal
            : recipient.LocalAccountId > 0;

    private static void AddResolvedRecipient(
        List<SmtpResolvedRecipient> recipients,
        SmtpResolvedRecipient recipient)
    {
        if (recipients.Any(existing =>
            existing.Address.Equals(recipient.Address, StringComparison.OrdinalIgnoreCase) &&
            existing.OriginalAddress.Equals(recipient.OriginalAddress, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        recipients.Add(recipient);
    }

    private static void AddFallbackRecipient(
        ExternalFetchAccountLease account,
        List<SmtpResolvedRecipient> recipients)
    {
        var fallbackAddress = GetFallbackRecipientAddress(account);
        if (string.IsNullOrWhiteSpace(fallbackAddress))
        {
            return;
        }

        recipients.Add(
            new SmtpResolvedRecipient(
                fallbackAddress,
                fallbackAddress,
                account.AccountId,
                IsLocal: true));
    }

    private static void AddCandidate(
        string address,
        List<string> candidates,
        HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        var normalized = address.Trim();
        if (seen.Add(normalized))
        {
            candidates.Add(normalized);
        }
    }

    private static bool TryExtractRecipientFromReceivedHeader(
        string headerValue,
        out string recipient)
    {
        recipient = string.Empty;
        var lastSemicolon = headerValue.LastIndexOf(';');
        if (lastSemicolon < 0)
        {
            return false;
        }

        var firstPart = headerValue[..lastSemicolon];
        var forPosition = firstPart.LastIndexOf("for ", StringComparison.OrdinalIgnoreCase);
        if (forPosition < 0)
        {
            return false;
        }

        recipient = firstPart[(forPosition + 4)..]
            .Trim(' ', '\r', '\n', '\t')
            .Replace("<", string.Empty, StringComparison.Ordinal)
            .Replace(">", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        return IsPlausibleEmailAddress(recipient);
    }

    private static bool IsPlausibleEmailAddress(string address)
    {
        var at = address.IndexOf('@');
        return at > 0 && at < address.Length - 1;
    }

    private static string GetFallbackRecipientAddress(ExternalFetchAccountLease account) =>
        string.IsNullOrWhiteSpace(account.AccountAddress)
            ? account.Username
            : account.AccountAddress;

    private static int ResolveDaysToKeep(
        int accountDaysToKeep,
        ExternalAccountDownloadScriptExecutionResult scriptResult) =>
        scriptResult.DeleteAction switch
        {
            ExternalAccountDownloadDeleteAction.DeleteImmediately => -1,
            ExternalAccountDownloadDeleteAction.DeleteAfterDays => scriptResult.DeleteAfterDays,
            ExternalAccountDownloadDeleteAction.NeverDelete => 0,
            _ => accountDaysToKeep
        };

    private static bool ShouldDeleteExistingRemoteMessage(
        int daysToKeep,
        DateTime createdAt,
        DateTimeOffset now)
    {
        if (daysToKeep == -1)
        {
            return true;
        }

        if (daysToKeep == 0)
        {
            return false;
        }

        var createdDate = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc).Date;
        var currentDate = now.UtcDateTime.Date;
        return (currentDate - createdDate).TotalDays > daysToKeep;
    }

    private async ValueTask ReleaseAccountSafelyAsync(
        int fetchAccountId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _accountStore.ReleaseAsync(fetchAccountId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
    }

    private sealed record AccountFetchResult(
        int MessagesDownloaded,
        int MessagesAccepted,
        int RemoteMessagesDeleted,
        int KnownUidsAdded,
        int KnownUidsDeleted);

    private sealed record NewMessageRetentionResult(
        int RemoteMessagesDeleted,
        int KnownUidsAdded);
}
