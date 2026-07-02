namespace HMailServer.Delivery;

public interface IDeliveryQueueBatchProcessor
{
    ValueTask<int> RunBatchAsync(
        DeliveryQueueProcessorOptions options,
        CancellationToken cancellationToken);
}
