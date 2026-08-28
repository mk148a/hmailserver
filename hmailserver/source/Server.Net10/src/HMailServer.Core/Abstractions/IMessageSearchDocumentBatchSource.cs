namespace HMailServer.Core.Abstractions;

public interface IMessageSearchDocumentBatchSource : IMessageSearchDocumentSource
{
    /// <summary>
    /// Yields exactly one positional result per input identity, using <see langword="null"/> when no document is available.
    /// </summary>
    IAsyncEnumerable<MessageSearchDocument?> TryLoadBatchAsync(
        IReadOnlyList<MessageIdentity> identities,
        CancellationToken cancellationToken);
}
