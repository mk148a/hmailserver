namespace HMailServer.Delivery;

public sealed record DomainConcurrencyOptions(
    int MaxConcurrentDeliveriesPerDomain)
{
    public static DomainConcurrencyOptions Default { get; } =
        new(MaxConcurrentDeliveriesPerDomain: 4);
}
