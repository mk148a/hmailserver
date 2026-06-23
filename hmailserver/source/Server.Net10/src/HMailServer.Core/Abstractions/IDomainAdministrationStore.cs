namespace HMailServer.Core.Abstractions;

public interface IDomainAdministrationStore
{
    ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
        CancellationToken cancellationToken);
}
