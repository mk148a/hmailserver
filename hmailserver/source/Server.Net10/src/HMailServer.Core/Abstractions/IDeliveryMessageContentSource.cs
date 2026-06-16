namespace HMailServer.Core.Abstractions;

public interface IDeliveryMessageContentSource
{
    ValueTask<byte[]?> TryLoadAsync(
        DeliveryQueuedMessage message,
        CancellationToken cancellationToken);
}
