namespace HMailServer.Core.Abstractions;

public interface IDeliveryTargetResolver
{
    ValueTask<IReadOnlyList<DeliveryTargetBatch>> ResolveAsync(
        DeliveryQueuedMessage message,
        CancellationToken cancellationToken);
}
