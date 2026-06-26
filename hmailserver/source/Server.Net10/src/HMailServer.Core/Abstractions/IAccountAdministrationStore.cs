namespace HMailServer.Core.Abstractions;

public interface IAccountAdministrationStore
{
    ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
        int domainId,
        CancellationToken cancellationToken);
}
