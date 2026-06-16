namespace HMailServer.Core.Abstractions;

public interface IDeliveryQueueRecipientStore
{
    ValueTask<bool> DeleteRecipientsAsync(
        long messageId,
        string leaseOwner,
        IReadOnlyList<long> recipientIds,
        CancellationToken cancellationToken);
}
