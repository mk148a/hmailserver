namespace HMailServer.Core.Abstractions;

public interface IDomainAliasAdministrationStore
{
    ValueTask<IReadOnlyList<DomainAliasAdministrationSnapshot>> GetDomainAliasesAsync(
        int domainId,
        CancellationToken cancellationToken);
}
