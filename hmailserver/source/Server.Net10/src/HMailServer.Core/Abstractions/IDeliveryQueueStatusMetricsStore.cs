namespace HMailServer.Core.Abstractions;

public interface IDeliveryQueueStatusMetricsStore
{
    ValueTask<DeliveryQueueStatusMetricsSnapshot> GetSnapshotAsync(
        DateTimeOffset sinceUtc,
        DateTimeOffset untilUtc,
        CancellationToken cancellationToken);
}
