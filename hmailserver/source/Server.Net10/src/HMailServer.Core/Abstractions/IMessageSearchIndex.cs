namespace HMailServer.Core.Abstractions;

public interface IMessageSearchIndex
{
    ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken);

    ValueTask QueueForIndexingAsync(MessageIdentity identity, CancellationToken cancellationToken);

    ValueTask UpsertAsync(MessageSearchDocument document, CancellationToken cancellationToken);

    IAsyncEnumerable<MessageIdentity> SearchAsync(
        ImapSearchRequest request,
        CancellationToken cancellationToken);
}
