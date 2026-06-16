namespace HMailServer.Core.Abstractions;

public interface IDeliveryQueueLeaseStore
{
    IAsyncEnumerable<MessageIdentity> LeaseReadyMessagesAsync(
        string leaseOwner,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    ValueTask<bool> CompleteAsync(
        long messageId,
        string leaseOwner,
        CancellationToken cancellationToken);

    ValueTask<bool> DeferAsync(
        long messageId,
        string leaseOwner,
        TimeSpan retryDelay,
        bool incrementRetryCount,
        CancellationToken cancellationToken);

    ValueTask<bool> ReleaseAsync(
        long messageId,
        string leaseOwner,
        CancellationToken cancellationToken);
}
