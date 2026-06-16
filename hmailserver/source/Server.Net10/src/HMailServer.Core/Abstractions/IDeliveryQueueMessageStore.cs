namespace HMailServer.Core.Abstractions;

public interface IDeliveryQueueMessageStore
{
    ValueTask<DeliveryQueuedMessage?> TryLoadAsync(
        MessageIdentity identity,
        string leaseOwner,
        CancellationToken cancellationToken);
}
