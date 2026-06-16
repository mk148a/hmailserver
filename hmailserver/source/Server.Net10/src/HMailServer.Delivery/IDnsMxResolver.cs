namespace HMailServer.Delivery;

public interface IDnsMxResolver
{
    ValueTask<IReadOnlyList<DnsMxRecord>> ResolveMxAsync(
        string domainName,
        CancellationToken cancellationToken);
}
