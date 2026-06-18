namespace HMailServer.Core.Abstractions;

public sealed record DeliveryQueueStatusKindMetric(
    string EventKind,
    long Count);
