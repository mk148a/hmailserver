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

    ValueTask<int> InsertFetchAccountForRestoreAsync(
        FetchAccountAdministrationDraft account,
        string encryptedPassword,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Fetch-account restore insertion is not available in this store.");

    ValueTask InsertFetchAccountUidAsync(
        int fetchAccountId,
        string uidValue,
        string uidTime,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Fetch-account UID insertion is not available in this store.");

    ValueTask DeleteFetchAccountAsync(
        int accountId,
        int fetchAccountId,
        CancellationToken cancellationToken);
}
