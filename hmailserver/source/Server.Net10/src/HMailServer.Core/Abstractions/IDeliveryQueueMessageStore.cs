namespace HMailServer.Core.Abstractions;

public interface IDeliveryQueueMessageStore
{
    ValueTask<DeliveryQueuedMessage?> TryLoadAsync(
        MessageIdentity identity,
        string leaseOwner,
        CancellationToken cancellationToken);

    ValueTask<bool> TryUpdateSizeAsync(
        DeliveryQueuedMessage message,
        long size,
        string leaseOwner,
        CancellationToken cancellationToken);
}
