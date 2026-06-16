namespace HMailServer.Core.Abstractions;

public interface IDeliveryTargetDispatcher
{
    ValueTask<DeliveryTargetDispatchResult> DispatchAsync(
        DeliveryQueuedMessage message,
        DeliveryTargetBatch targetBatch,
        CancellationToken cancellationToken);
}
