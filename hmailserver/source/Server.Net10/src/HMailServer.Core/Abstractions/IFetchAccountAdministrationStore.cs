namespace HMailServer.Core.Abstractions;

public interface IFetchAccountAdministrationStore
{
    ValueTask<IReadOnlyList<FetchAccountAdministrationSnapshot>> GetFetchAccountsAsync(
        int accountId,
        CancellationToken cancellationToken);
}
