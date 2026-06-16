using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class LocalDeliveryTargetDispatcher : IDeliveryTargetDispatcher
{
    private readonly ILocalDeliveryStore _localDeliveryStore;

    public LocalDeliveryTargetDispatcher(ILocalDeliveryStore localDeliveryStore)
    {
        _localDeliveryStore = localDeliveryStore;
    }

    public async ValueTask<DeliveryTargetDispatchResult> DispatchAsync(
        DeliveryQueuedMessage message,
        DeliveryTargetBatch targetBatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(targetBatch);

        if (targetBatch.Target.Kind != DeliveryTargetKind.LocalAccount)
        {
            return DeliveryTargetDispatchResult.TransientFailure(
                "Delivery target is not handled by the local delivery dispatcher.");
        }

        try
        {
            await _localDeliveryStore.DeliverAsync(message, targetBatch, cancellationToken).ConfigureAwait(false);
            return DeliveryTargetDispatchResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DeliveryTargetDispatchResult.TransientFailure(ex.Message);
        }
    }
}
