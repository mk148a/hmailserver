namespace HMailServer.Delivery;

public sealed record DeliveryQueueWorkerOptions(
    TimeSpan IdleWait,
    TimeSpan FailureWait)
{
    public static DeliveryQueueWorkerOptions Default { get; } = new(
        IdleWait: TimeSpan.FromMinutes(1),
        FailureWait: TimeSpan.FromSeconds(5));
}
