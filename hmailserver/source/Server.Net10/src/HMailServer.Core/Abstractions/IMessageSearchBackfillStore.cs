namespace HMailServer.Core.Abstractions;

public interface IMessageSearchBackfillStore
{
    IAsyncEnumerable<MessageIdentity> LeaseBatchAsync(
        string leaseOwner,
        int batchSize,
        TimeSpan leaseDuration,
        int maxAttempts,
        CancellationToken cancellationToken);

    ValueTask MarkSucceededAsync(
        MessageIdentity identity,
        string leaseOwner,
        CancellationToken cancellationToken);

    ValueTask MarkFailedAsync(
        MessageIdentity identity,
        string leaseOwner,
        string error,
        TimeSpan retryDelay,
        CancellationToken cancellationToken);
}
