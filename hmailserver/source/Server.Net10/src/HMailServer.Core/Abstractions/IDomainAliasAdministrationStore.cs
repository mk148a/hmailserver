namespace HMailServer.Core.Abstractions;

public interface IDomainAliasAdministrationStore
{
    ValueTask<IReadOnlyList<DomainAliasAdministrationSnapshot>> GetDomainAliasesAsync(
        int domainId,
        CancellationToken cancellationToken);

    ValueTask<int> InsertDomainAliasAsync(
        int owningDomainId,
        DomainAliasAdministrationSnapshot alias,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Domain alias insertion is not implemented by this store.");
}
