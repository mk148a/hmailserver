namespace HMailServer.Core.Abstractions;

public interface IDeliveryQueueAdministrationStore
{
    ValueTask<bool> ResetDeliveryTimeAsync(
        long messageId,
        CancellationToken cancellationToken);
}
