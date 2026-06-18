namespace HMailServer.Core.Abstractions;

public interface IDeliveryQueueStatusObserver
{
    ValueTask RecordAsync(
        DeliveryQueueStatusEvent statusEvent,
        CancellationToken cancellationToken);
}
