namespace HMailServer.Core.Abstractions;

public interface IAliasAdministrationStore
{
    ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(
        int domainId,
        CancellationToken cancellationToken);
}
