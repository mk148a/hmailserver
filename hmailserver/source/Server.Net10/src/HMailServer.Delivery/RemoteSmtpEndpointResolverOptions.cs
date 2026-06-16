namespace HMailServer.Delivery;

public sealed record RemoteSmtpEndpointResolverOptions(
    TimeSpan DefaultCacheTtl,
    TimeSpan NegativeCacheTtl)
{
    public static RemoteSmtpEndpointResolverOptions Default { get; } =
        new(
            DefaultCacheTtl: TimeSpan.FromMinutes(10),
            NegativeCacheTtl: TimeSpan.FromMinutes(2));
}
