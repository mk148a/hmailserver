namespace HMailServer.Delivery;

public sealed record DeliveryQueueClearOptions(int BatchSize)
{
    public static DeliveryQueueClearOptions Default { get; } = new(BatchSize: 500);
}
