using System.Globalization;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapCopyCommandHandler
{
    private readonly ImapCopyCommandParser _parser;
    private readonly IImapMailboxStore _mailboxStore;
    private readonly IImapMessageCopyStore _copyStore;

    public ImapCopyCommandHandler(
        ImapCopyCommandParser parser,
        IImapMailboxStore mailboxStore,
        IImapMessageCopyStore copyStore)
    {
        _parser = parser;
        _mailboxStore = mailboxStore;
        _copyStore = copyStore;
    }

    public async ValueTask<string> HandleAsync(
        int requesterAccountId,
        int sourceAccountId,
        int sourceFolderId,
        string tag,
        string arguments,
        bool useUid,
        bool deleteSource,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(
            requesterAccountId,
            sourceAccountId,
            sourceFolderId,
            tag,
            arguments,
            useUid,
            deleteSource,
            cancellationToken).ConfigureAwait(false);
        return result.Response;
    }

    public async ValueTask<ImapCopyCommandResult> ExecuteAsync(
        int requesterAccountId,
        int sourceAccountId,
        int sourceFolderId,
        string tag,
        string arguments,
        bool useUid,
        bool deleteSource,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(arguments);

        ImapCopyCommand command;
        try
        {
            command = _parser.Parse(arguments);
        }
        catch (ImapCopyParseException ex)
        {
            return Failure($"{SanitizeAtom(tag)} BAD {SanitizeResponseText(ex.Message)}\r\n", deleteSource);
        }

        var destination = await _mailboxStore
            .SelectMailboxAsync(requesterAccountId, command.DestinationMailbox, readOnly: false, cancellationToken)
            .ConfigureAwait(false);
        if (destination is null)
        {
            return Failure($"{SanitizeAtom(tag)} NO Can't find mailbox with that name.\r\n", deleteSource);
        }

        if (destination.IsReadOnly)
        {
            return Failure($"{SanitizeAtom(tag)} NO Destination mailbox is read-only.\r\n", deleteSource);
        }

        if ((destination.AclRights & ImapAclRights.Insert) != ImapAclRights.Insert)
        {
            return Failure($"{SanitizeAtom(tag)} NO ACL: Insert permission denied (Required for COPY command).\r\n", deleteSource);
        }

        var request = new ImapCopyRequest(
            sourceAccountId,
            sourceFolderId,
            destination.AccountId,
            destination.FolderId,
            command.MessageSet,
            useUid,
            deleteSource);

        var builder = new StringBuilder();
        var copiedMessages = new List<ImapCopiedMessage>();
        try
        {
            await foreach (var message in _copyStore.CopyAsync(request, cancellationToken).ConfigureAwait(false))
            {
                copiedMessages.Add(message);
                if (deleteSource && message.ExpungeSequenceNumber is { } sequenceNumber)
                {
                    builder.Append("* ")
                        .Append(sequenceNumber.ToString(CultureInfo.InvariantCulture))
                        .Append(" EXPUNGE\r\n");
                }
            }
        }
        catch (IOException ex)
        {
            return Failure($"{SanitizeAtom(tag)} NO {SanitizeResponseText(ex.Message)}\r\n", deleteSource);
        }
        catch (InvalidOperationException ex)
        {
            return Failure($"{SanitizeAtom(tag)} NO {SanitizeResponseText(ex.Message)}\r\n", deleteSource);
        }

        builder.Append(SanitizeAtom(tag))
            .Append(deleteSource ? " OK MOVE completed\r\n" : " OK COPY completed\r\n");
        return new ImapCopyCommandResult(
            builder.ToString(),
            copiedMessages,
            destination.AccountId,
            destination.FolderId,
            deleteSource);
    }

    private static ImapCopyCommandResult Failure(string response, bool deleteSource) =>
        new(
            response,
            Array.Empty<ImapCopiedMessage>(),
            DestinationAccountId: null,
            DestinationFolderId: null,
            deleteSource);

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
