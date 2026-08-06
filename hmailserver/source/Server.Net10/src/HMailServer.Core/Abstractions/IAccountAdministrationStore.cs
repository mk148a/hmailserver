namespace HMailServer.Core.Abstractions;

public interface IAccountAdministrationStore
{
    ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
        int domainId,
        CancellationToken cancellationToken);

    ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(
        int accountId,
        CancellationToken cancellationToken);

    ValueTask<int> InsertAccountAsync(
        int domainId,
        AccountAdministrationSnapshot snapshot,
        string password,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Account insertion is not available in this store.");

    ValueTask<bool> DeleteAccountAsync(
        int domainId,
        int accountId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Account deletion is not available in this store.");

    ValueTask<bool> UpdateAccountAsync(
        int domainId,
        AccountAdministrationSnapshot snapshot,
        string? password,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Account update is not available in this store.");
}