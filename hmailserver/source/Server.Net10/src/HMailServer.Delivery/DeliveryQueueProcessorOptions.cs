namespace HMailServer.Delivery;

public sealed record DeliveryQueueProcessorOptions(
    string LeaseOwner,
    int BatchSize,
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay,
    int MaxRetries,
    TimeSpan MaxRetryDelay)
{
    public static DeliveryQueueProcessorOptions Default(string leaseOwner) =>
        new(
            leaseOwner,
            BatchSize: 50,
            LeaseDuration: TimeSpan.FromMinutes(5),
            RetryDelay: TimeSpan.FromMinutes(5),
            MaxRetries: 4,
            MaxRetryDelay: TimeSpan.FromHours(4));
}
