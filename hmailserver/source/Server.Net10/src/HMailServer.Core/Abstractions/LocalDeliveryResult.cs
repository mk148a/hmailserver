namespace HMailServer.Core.Abstractions;

public sealed record LocalDeliveryResult(
    MessageIdentity DeliveredMessage,
    int RecipientCount);
