namespace HMailServer.Core.Abstractions;

public interface IBackupFetchAccountAdministrationStore
{
    ValueTask<IReadOnlyList<FetchAccountBackupAdministrationSnapshot>> GetBackupFetchAccountsAsync(
        int accountId,
        CancellationToken cancellationToken);
}
