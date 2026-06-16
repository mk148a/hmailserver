using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapSortExecutor
{
    private readonly IMessageSortIndex _sortIndex;
    private readonly IImapSequenceNumberResolver? _sequenceNumberResolver;

    public ImapSortExecutor(
        IMessageSortIndex sortIndex,
        IImapSequenceNumberResolver? sequenceNumberResolver = null)
    {
        _sortIndex = sortIndex;
        _sequenceNumberResolver = sequenceNumberResolver;
    }

    public async ValueTask<string> ExecuteAsync(
        ImapSortRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var matches = new List<MessageIdentity>();
        await foreach (var identity in _sortIndex.SortAsync(request, cancellationToken).ConfigureAwait(false))
        {
            matches.Add(identity);
        }

        var identifiers = request.ReturnUid
            ? SelectUids(matches)
            : await ResolveSequenceNumbersAsync(request, matches, cancellationToken).ConfigureAwait(false);
        return ImapSortResultFormatter.Format(identifiers);
    }

    private static IReadOnlyList<long> SelectUids(IReadOnlyList<MessageIdentity> matches)
    {
        var identifiers = new long[matches.Count];
        for (var index = 0; index < matches.Count; index++)
        {
            identifiers[index] = matches[index].Uid;
        }

        return identifiers;
    }

    private async ValueTask<IReadOnlyList<long>> ResolveSequenceNumbersAsync(
        ImapSortRequest request,
        IReadOnlyList<MessageIdentity> matches,
        CancellationToken cancellationToken)
    {
        if (_sequenceNumberResolver is null)
        {
            throw new InvalidOperationException("Non-UID IMAP SORT requires a mailbox sequence number resolver.");
        }

        var sequenceNumbers = await _sequenceNumberResolver
            .ResolveMailboxSequenceNumbersAsync(
                request.SearchRequest.AccountId,
                request.SearchRequest.FolderId,
                cancellationToken)
            .ConfigureAwait(false);

        var identifiers = new long[matches.Count];
        for (var index = 0; index < matches.Count; index++)
        {
            var identity = matches[index];
            if (!sequenceNumbers.TryGetValue(identity.MessageId, out var sequenceNumber))
            {
                throw new InvalidOperationException(
                    $"Message {identity.MessageId} was returned by SORT but is not present in the mailbox sequence snapshot.");
            }

            identifiers[index] = sequenceNumber;
        }

        return identifiers;
    }
}
