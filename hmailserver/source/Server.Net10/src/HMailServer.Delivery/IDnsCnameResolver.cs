namespace HMailServer.Delivery;

public interface IDnsCnameResolver
{
    ValueTask<IReadOnlyList<DnsCnameRecord>> ResolveCnameAsync(
        string domainName,
        CancellationToken cancellationToken);
}
