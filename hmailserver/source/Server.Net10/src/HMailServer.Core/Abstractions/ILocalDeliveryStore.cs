namespace HMailServer.Core.Abstractions;

public interface ILocalDeliveryStore
{
    ValueTask<LocalDeliveryResult> DeliverAsync(
        DeliveryQueuedMessage message,
        DeliveryTargetBatch targetBatch,
        CancellationToken cancellationToken);
}
