namespace HMailServer.Core.Abstractions;

public sealed record DeliveryTargetBatch(
    DeliveryTarget Target,
    IReadOnlyList<DeliveryQueueRecipient> Recipients);
