namespace HMailServer.Core.Abstractions;

public interface IDkimSigner
{
    ValueTask<byte[]?> SignAsync(
        DeliveryQueuedMessage message,
        byte[] messageData,
        CancellationToken cancellationToken);
}
