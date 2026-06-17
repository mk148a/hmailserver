namespace HMailServer.Core.Abstractions;

public interface IDeliveryMessageContentStore : IDeliveryMessageContentSource
{
    ValueTask<bool> TrySaveAsync(
        DeliveryQueuedMessage message,
        byte[] messageData,
        CancellationToken cancellationToken);
}
