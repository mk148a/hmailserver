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

        var identifiers = request.ReturnUid
            ? await SelectUidsAsync(request, cancellationToken).ConfigureAwait(false)
            : await ResolveSequenceNumbersAsync(request, cancellationToken).ConfigureAwait(false);
        return ImapSortResultFormatter.Format(identifiers);
    }

    private async ValueTask<IReadOnlyList<long>> SelectUidsAsync(
        ImapSortRequest request,
        CancellationToken cancellationToken)
    {
        var identifiers = new List<long>();
        await foreach (var identity in _sortIndex.SortAsync(request, cancellationToken).ConfigureAwait(false))
        {
            identifiers.Add(identity.Uid);
        }

        return identifiers;
    }

    private async ValueTask<IReadOnlyList<long>> ResolveSequenceNumbersAsync(
        ImapSortRequest request,
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

        var identifiers = new List<long>();
        await foreach (var identity in _sortIndex.SortAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (!sequenceNumbers.TryGetValue(identity.MessageId, out var sequenceNumber))
            {
                throw new InvalidOperationException(
                    $"Message {identity.MessageId} was returned by SORT but is not present in the mailbox sequence snapshot.");
            }

            identifiers.Add(sequenceNumber);
        }

        return identifiers;
    }
}
