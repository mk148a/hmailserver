using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class CompositeDeliveryTargetDispatcher : IDeliveryTargetDispatcher
{
    private readonly IDeliveryTargetDispatcher _localDispatcher;
    private readonly IDeliveryTargetDispatcher _remoteDispatcher;

    public CompositeDeliveryTargetDispatcher(
        IDeliveryTargetDispatcher localDispatcher,
        IDeliveryTargetDispatcher remoteDispatcher)
    {
        _localDispatcher = localDispatcher;
        _remoteDispatcher = remoteDispatcher;
    }

    public ValueTask<DeliveryTargetDispatchResult> DispatchAsync(
        DeliveryQueuedMessage message,
        DeliveryTargetBatch targetBatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetBatch);

        return targetBatch.Target.Kind == DeliveryTargetKind.LocalAccount
            ? _localDispatcher.DispatchAsync(message, targetBatch, cancellationToken)
            : _remoteDispatcher.DispatchAsync(message, targetBatch, cancellationToken);
    }
}
