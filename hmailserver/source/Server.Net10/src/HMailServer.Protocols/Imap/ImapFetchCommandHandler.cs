using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapFetchCommandHandler
{
    private readonly ImapFetchCommandParser _parser;
    private readonly IImapMessageFetchStore _fetchStore;
    private readonly IImapMessageMutationStore? _mutationStore;

    public ImapFetchCommandHandler(
        ImapFetchCommandParser parser,
        IImapMessageFetchStore fetchStore,
        IImapMessageMutationStore? mutationStore = null)
    {
        _parser = parser;
        _fetchStore = fetchStore;
        _mutationStore = mutationStore;
    }

    public async ValueTask<byte[]> HandleAsync(
        int accountId,
        int folderId,
        string tag,
        string arguments,
        bool useUid,
        CancellationToken cancellationToken,
        bool isReadOnly = false,
        long aclRights = ImapAclRights.All,
        int? requesterAccountId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            var request = _parser.Parse(accountId, folderId, arguments, useUid);
            var messages = new List<ImapFetchedMessage>();
            await foreach (var message in _fetchStore.FetchAsync(request, cancellationToken).ConfigureAwait(false))
            {
                messages.Add(message);
            }

            await MarkSeenAsync(request, messages, isReadOnly, aclRights, requesterAccountId, cancellationToken).ConfigureAwait(false);

            return ImapFetchResponseFormatter.Format(messages, request.Items, tag);
        }
        catch (ImapFetchParseException ex)
        {
            return Encode($"{SanitizeAtom(tag)} BAD {SanitizeResponseText(ex.Message)}\r\n");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Encode($"{SanitizeAtom(tag)} NO {SanitizeResponseText(ex.Message)}\r\n");
        }
        catch (InvalidOperationException ex)
        {
            return Encode($"{SanitizeAtom(tag)} NO {SanitizeResponseText(ex.Message)}\r\n");
        }
        catch (IOException ex)
        {
            return Encode($"{SanitizeAtom(tag)} NO {SanitizeResponseText(ex.Message)}\r\n");
        }
    }

    private async ValueTask MarkSeenAsync(
        ImapFetchRequest request,
        IReadOnlyList<ImapFetchedMessage> messages,
        bool isReadOnly,
        long aclRights,
        int? requesterAccountId,
        CancellationToken cancellationToken)
    {
        if (!request.MarksSeen || isReadOnly || _mutationStore is null ||
            (aclRights & ImapAclRights.WriteSeen) != ImapAclRights.WriteSeen)
        {
            return;
        }

        foreach (var message in messages)
        {
            if ((message.Flags & ImapMessageFlags.Seen) == ImapMessageFlags.Seen)
            {
                continue;
            }

            var storeRequest = new ImapStoreRequest(
                request.AccountId,
                request.FolderId,
                [new ImapIdRange(message.Identity.Uid, message.Identity.Uid)],
                UseUid: true,
                Mode: ImapStoreMode.Add,
                Flags: ImapMessageFlags.Seen,
                Silent: true,
                RequesterAccountId: requesterAccountId);
            await foreach (var _ in _mutationStore.StoreFlagsAsync(storeRequest, cancellationToken).ConfigureAwait(false))
            {
            }
        }
    }

    private static byte[] Encode(string value) => System.Text.Encoding.ASCII.GetBytes(value);

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
