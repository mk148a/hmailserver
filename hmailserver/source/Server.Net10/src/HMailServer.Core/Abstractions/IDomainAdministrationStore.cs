namespace HMailServer.Core.Abstractions;

public interface IDomainAdministrationStore
{
    ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
        CancellationToken cancellationToken);

    ValueTask<int> InsertDomainAsync(
        DomainAdministrationSnapshot snapshot,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Domain insertion is not available in this store.");
}