using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class ServerStatusDeliveryQueueStatusObserver : IDeliveryQueueStatusObserver
{
    private readonly IDeliveryQueueStatusObserver _inner;
    private readonly ServerStatusRuntimeState _runtimeState;

    public ServerStatusDeliveryQueueStatusObserver(
        IDeliveryQueueStatusObserver inner,
        ServerStatusRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(runtimeState);
        _inner = inner;
        _runtimeState = runtimeState;
    }

    public async ValueTask RecordAsync(
        DeliveryQueueStatusEvent statusEvent,
        CancellationToken cancellationToken)
    {
        await _inner.RecordAsync(statusEvent, cancellationToken).ConfigureAwait(false);

        if (statusEvent.Kind == DeliveryQueueStatusEventKind.MessageCompleted)
        {
            _runtimeState.OnMessageProcessed();
        }
    }
}
