namespace HMailServer.Delivery;

public sealed record RemoteDeliveryOptions(
    string HeloHost,
    TimeSpan RetryDelay)
{
    public static RemoteDeliveryOptions Default(string hostName) =>
        new(
            string.IsNullOrWhiteSpace(hostName) ? "localhost" : hostName,
            RetryDelay: TimeSpan.FromMinutes(5));
}
