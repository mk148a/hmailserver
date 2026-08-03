namespace HMailServer.Core.Abstractions;

public interface IFetchAccountAdministrationStore
{
    ValueTask<IReadOnlyList<FetchAccountAdministrationSnapshot>> GetFetchAccountsAsync(
        int accountId,
        CancellationToken cancellationToken);

    ValueTask SetRetryNowAsync(
        int accountId,
        int fetchAccountId,
        CancellationToken cancellationToken);

    ValueTask<int> InsertFetchAccountAsync(
        FetchAccountAdministrationDraft account,
        CancellationToken cancellationToken);

    ValueTask DeleteFetchAccountAsync(
        int accountId,
        int fetchAccountId,
        CancellationToken cancellationToken);
}
