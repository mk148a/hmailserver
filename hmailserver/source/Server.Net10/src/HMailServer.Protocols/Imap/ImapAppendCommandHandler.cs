using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapAppendCommandHandler
{
    private readonly ImapAppendCommandParser _parser;
    private readonly IImapMailboxStore _mailboxStore;
    private readonly IImapMessageAppendStore _appendStore;

    public ImapAppendCommandHandler(
        ImapAppendCommandParser parser,
        IImapMailboxStore mailboxStore,
        IImapMessageAppendStore appendStore)
    {
        _parser = parser;
        _mailboxStore = mailboxStore;
        _appendStore = appendStore;
    }

    public ImapAppendCommand Parse(string arguments) => _parser.Parse(arguments);

    public ValueTask<ImapMailboxSelection?> ResolveDestinationAsync(
        int requesterAccountId,
        string mailboxName,
        CancellationToken cancellationToken) =>
        _mailboxStore.SelectMailboxAsync(requesterAccountId, mailboxName, readOnly: false, cancellationToken);

    public async ValueTask<string> HandleAsync(
        int requesterAccountId,
        string tag,
        ImapAppendCommand command,
        byte[] rawMessage,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(
            requesterAccountId,
            tag,
            command,
            rawMessage,
            cancellationToken).ConfigureAwait(false);
        return result.Response;
    }

    public async ValueTask<ImapAppendCommandResult> ExecuteAsync(
        int requesterAccountId,
        string tag,
        ImapAppendCommand command,
        byte[] rawMessage,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(rawMessage);

        var destination = await ResolveDestinationAsync(requesterAccountId, command.MailboxName, cancellationToken)
            .ConfigureAwait(false);
        if (destination is null)
        {
            return Failure($"{SanitizeAtom(tag)} NO Can't find mailbox with that name.\r\n");
        }

        if (destination.IsReadOnly)
        {
            return Failure($"{SanitizeAtom(tag)} NO Destination mailbox is read-only.\r\n");
        }

        if ((destination.AclRights & ImapAclRights.Insert) != ImapAclRights.Insert)
        {
            return Failure($"{SanitizeAtom(tag)} NO ACL: Insert permission denied (Required for APPEND command).\r\n");
        }

        var flags = command.Flags;
        if ((destination.AclRights & ImapAclRights.WriteSeen) != ImapAclRights.WriteSeen)
        {
            flags = (byte)(flags & ~ImapMessageFlags.Seen);
        }

        try
        {
            var result = await _appendStore
                .AppendAsync(
                    new ImapAppendRequest(
                        destination.AccountId,
                        destination.FolderId,
                        command.MailboxName,
                        flags,
                        command.InternalDateUtc,
                        rawMessage),
                    cancellationToken)
                .ConfigureAwait(false);

            return new ImapAppendCommandResult(
                $"{SanitizeAtom(tag)} OK [APPENDUID {result.UidValidity} {result.Identity.Uid}] APPEND completed\r\n",
                result,
                destination.AccountId,
                destination.FolderId);
        }
        catch (IOException ex)
        {
            return Failure($"{SanitizeAtom(tag)} NO {SanitizeResponseText(ex.Message)}\r\n");
        }
        catch (InvalidOperationException ex)
        {
            return Failure($"{SanitizeAtom(tag)} NO {SanitizeResponseText(ex.Message)}\r\n");
        }
    }

    private static ImapAppendCommandResult Failure(string response) =>
        new(response, AppendResult: null, DestinationAccountId: null, DestinationFolderId: null);

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
