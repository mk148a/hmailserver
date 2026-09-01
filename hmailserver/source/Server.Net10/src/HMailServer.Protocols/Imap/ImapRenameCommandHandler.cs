using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapRenameCommandHandler
{
    private readonly IImapMailboxStore _mailboxStore;
    private readonly IImapFolderRenameStore _renameStore;
    private readonly string _hierarchyDelimiter;
    private readonly string _publicFolderName;
    private readonly IImapFolderChangeTracker _folderChangeTracker;

    public ImapRenameCommandHandler(
        IImapMailboxStore mailboxStore,
        IImapFolderRenameStore renameStore,
        string hierarchyDelimiter,
        string publicFolderName,
        IImapFolderChangeTracker? folderChangeTracker = null)
    {
        _mailboxStore = mailboxStore;
        _renameStore = renameStore;
        ArgumentException.ThrowIfNullOrWhiteSpace(hierarchyDelimiter);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicFolderName);
        _hierarchyDelimiter = hierarchyDelimiter;
        _publicFolderName = publicFolderName;
        _folderChangeTracker = folderChangeTracker ?? ImapFolderChangeTracker.Shared;
    }

    public async ValueTask<string> HandleAsync(
        int accountId,
        string tag,
        string arguments,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(arguments);

        IReadOnlyList<string> parsedArguments;
        try
        {
            parsedArguments = ImapCommandArguments.Parse(arguments);
        }
        catch (ImapSearchParseException ex)
        {
            return TaggedBad(tag, ex.Message);
        }

        if (parsedArguments.Count != 2)
        {
            return TaggedBad(tag, "RENAME command requires 2 parameters.");
        }

        var sourceName = parsedArguments[0];
        var destinationName = parsedArguments[1];
        if (sourceName.Equals("INBOX", StringComparison.OrdinalIgnoreCase) ||
            destinationName.Equals("INBOX", StringComparison.OrdinalIgnoreCase))
        {
            return TaggedNo(tag, "Cannot rename INBOX.");
        }

        if (!IsRootPrivateFolderName(sourceName) ||
            !IsRootPrivateFolderName(destinationName))
        {
            return TaggedNo(tag, "RENAME supports root-level private folders only.");
        }

        var sourceMailbox = await _mailboxStore
            .SelectMailboxAsync(accountId, sourceName, readOnly: false, cancellationToken)
            .ConfigureAwait(false);
        if (sourceMailbox is null)
        {
            return TaggedBad(tag, "Folder could not be found.");
        }

        if ((sourceMailbox.AclRights & ImapAclRights.DeleteMailbox) == 0)
        {
            return TaggedNo(tag, "ACL DeleteMailbox permission denied (required for RENAME).");
        }

        var result = await _renameStore
            .RenameRootFolderAsync(accountId, sourceName, destinationName, cancellationToken)
            .ConfigureAwait(false);
        if (result.Status == ImapFolderRenameStatus.Success && result.Folder is { } folder)
        {
            _folderChangeTracker.PublishUpsert(folder);
            return TaggedOk(tag);
        }

        return result.Status switch
        {
            ImapFolderRenameStatus.FolderNotFound => TaggedBad(tag, "Folder could not be found."),
            ImapFolderRenameStatus.TargetExists => TaggedNo(tag, "Target folder already exist."),
            ImapFolderRenameStatus.PermissionDenied => TaggedNo(
                tag,
                "ACL DeleteMailbox permission denied (required for RENAME)."),
            _ => TaggedNo(tag, "RENAME failed")
        };
    }

    private bool IsRootPrivateFolderName(string value) =>
        value.Length is > 0 and <= 255 &&
        !value.Contains(_hierarchyDelimiter, StringComparison.Ordinal) &&
        !value.Equals(_publicFolderName, StringComparison.OrdinalIgnoreCase) &&
        value[0] != '#';

    private static string TaggedOk(string tag) =>
        $"{SanitizeAtom(tag)} OK Rename completed\r\n";

    private static string TaggedNo(string tag, string response) =>
        $"{SanitizeAtom(tag)} NO {SanitizeResponseText(response)}\r\n";

    private static string TaggedBad(string tag, string response) =>
        $"{SanitizeAtom(tag)} BAD {SanitizeResponseText(response)}\r\n";

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
