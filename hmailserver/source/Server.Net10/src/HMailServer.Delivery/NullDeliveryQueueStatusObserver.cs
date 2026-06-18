using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class NullDeliveryQueueStatusObserver : IDeliveryQueueStatusObserver
{
    public static NullDeliveryQueueStatusObserver Instance { get; } = new();

    private NullDeliveryQueueStatusObserver()
    {
    }

    public ValueTask RecordAsync(
        DeliveryQueueStatusEvent statusEvent,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
