using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapSubscriptionCommandHandler
{
    private readonly IImapMailboxSubscriptionStore _store;
    private readonly string _publicFolderName;

    public ImapSubscriptionCommandHandler(IImapMailboxSubscriptionStore store, string publicFolderName)
    {
        _store = store;
        ArgumentException.ThrowIfNullOrWhiteSpace(publicFolderName);
        _publicFolderName = publicFolderName;
    }

    public async ValueTask<string> HandleAsync(
        int accountId,
        string tag,
        string command,
        string arguments,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
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

        if (command.Equals("SUBSCRIBE", StringComparison.Ordinal))
        {
            if (parsedArguments.Count < 1)
            {
                return TaggedBad(tag, "Command requires at least 1 parameter.");
            }

            var mailboxName = parsedArguments[0];
            if (string.Equals(mailboxName, _publicFolderName, StringComparison.Ordinal))
            {
                return TaggedOk(tag, "Subscribe");
            }

            return FormatResult(
                tag,
                "Subscribe",
                await _store.SetSubscribedAsync(accountId, mailboxName, true, cancellationToken).ConfigureAwait(false));
        }

        if (command.Equals("UNSUBSCRIBE", StringComparison.Ordinal))
        {
            if (parsedArguments.Count != 1)
            {
                return TaggedBad(tag, "Command requires 1 parameter.");
            }

            return FormatResult(
                tag,
                "Unsubscribe",
                await _store.SetSubscribedAsync(accountId, parsedArguments[0], false, cancellationToken).ConfigureAwait(false));
        }

        return TaggedBad(tag, "Unsupported subscription command");
    }

    private static string FormatResult(string tag, string command, ImapMailboxSubscriptionResult result) =>
        result.Status switch
        {
            ImapMailboxSubscriptionStatus.Success => TaggedOk(tag, command),
            ImapMailboxSubscriptionStatus.MailboxNotFound => command.Equals("Subscribe", StringComparison.Ordinal)
                ? TaggedNo(tag, "Folder could not be found.")
                : TaggedNo(tag, "That mailbox does not exist."),
            ImapMailboxSubscriptionStatus.PermissionDenied => TaggedNo(
                tag,
                "ACL: Lookup permission denied (required for SUBSCRIBE)."),
            ImapMailboxSubscriptionStatus.PublicFolderNotSupported => TaggedNo(
                tag,
                "It is not possible to unsubscribe from public folders."),
            _ => TaggedNo(tag, $"{command.ToUpperInvariant()} failed")
        };

    private static string TaggedOk(string tag, string command) =>
        $"{SanitizeAtom(tag)} OK {command} completed\r\n";

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
