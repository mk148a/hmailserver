namespace HMailServer.Storage.SqlServer;

public sealed record DeliveryQueueStatusMaintenanceOptions(
    bool Enabled,
    TimeSpan Retention,
    TimeSpan CleanupInterval,
    int BatchSize)
{
    public static DeliveryQueueStatusMaintenanceOptions Disabled { get; } =
        new(
            Enabled: false,
            Retention: TimeSpan.Zero,
            CleanupInterval: TimeSpan.FromHours(1),
            BatchSize: 5000);
}
