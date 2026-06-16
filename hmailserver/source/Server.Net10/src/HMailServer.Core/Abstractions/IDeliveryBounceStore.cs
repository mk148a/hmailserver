namespace HMailServer.Core.Abstractions;

public interface IDeliveryBounceStore
{
    ValueTask<DeliveryBounceResult> SubmitBounceAsync(
        DeliveryQueuedMessage originalMessage,
        IReadOnlyList<DeliveryQueueRecipient> failedRecipients,
        string failureDescription,
        CancellationToken cancellationToken);
}
