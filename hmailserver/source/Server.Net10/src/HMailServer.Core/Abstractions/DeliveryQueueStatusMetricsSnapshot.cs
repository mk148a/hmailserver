namespace HMailServer.Core.Abstractions;

public sealed record DeliveryQueueStatusMetricsSnapshot(
    DateTimeOffset SinceUtc,
    DateTimeOffset UntilUtc,
    IReadOnlyList<DeliveryQueueStatusKindMetric> CountsByKind);
