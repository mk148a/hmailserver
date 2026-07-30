namespace HMailServer.Core.Abstractions;

public interface IBackupAccountAdministrationStore
{
    ValueTask<IReadOnlyList<AccountBackupAdministrationSnapshot>> GetBackupAccountsAsync(
        int domainId,
        CancellationToken cancellationToken);
}
