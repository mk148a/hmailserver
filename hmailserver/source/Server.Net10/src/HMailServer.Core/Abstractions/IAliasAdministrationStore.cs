namespace HMailServer.Core.Abstractions;

public interface IAliasAdministrationStore
{
    ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(
        int domainId,
        CancellationToken cancellationToken);

    ValueTask<int> InsertAliasAsync(
        int owningDomainId,
        AliasAdministrationSnapshot alias,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Alias insertion is not implemented by this store.");

    ValueTask UpdateAliasAsync(
        int owningDomainId,
        AliasAdministrationSnapshot alias,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Alias updates are not implemented by this store.");
}
