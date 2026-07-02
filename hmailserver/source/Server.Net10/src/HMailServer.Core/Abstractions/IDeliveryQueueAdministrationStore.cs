namespace HMailServer.Core.Abstractions;

public interface IDeliveryQueueAdministrationStore
{
    ValueTask<bool> ResetDeliveryTimeAsync(
        long messageId,
        CancellationToken cancellationToken);

    ValueTask<bool> RemoveAsync(
        long messageId,
        CancellationToken cancellationToken);

    ValueTask<int> ClearBatchAsync(
        int batchSize,
        CancellationToken cancellationToken);
}
