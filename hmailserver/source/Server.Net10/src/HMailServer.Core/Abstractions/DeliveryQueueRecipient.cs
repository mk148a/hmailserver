namespace HMailServer.Core.Abstractions;

public sealed record DeliveryQueueRecipient(
    long RecipientId,
    string Address,
    string OriginalAddress,
    int LocalAccountId);
