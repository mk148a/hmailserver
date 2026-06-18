namespace HMailServer.Core.Abstractions;

public interface IExternalFetchAccountStore
{
    IAsyncEnumerable<ExternalFetchAccountLease> LeaseReadyAccountsAsync(
        int batchSize,
        CancellationToken cancellationToken);

    ValueTask<int> DeferInactiveAccountsAsync(CancellationToken cancellationToken);

    ValueTask<bool> CompleteAsync(
        int fetchAccountId,
        CancellationToken cancellationToken);

    ValueTask<bool> ReleaseAsync(
        int fetchAccountId,
        CancellationToken cancellationToken);

    ValueTask ResetLocksAsync(CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ExternalFetchKnownUid>> LoadKnownUidsAsync(
        int fetchAccountId,
        CancellationToken cancellationToken);

    ValueTask AddKnownUidAsync(
        int fetchAccountId,
        string uid,
        CancellationToken cancellationToken);

    ValueTask<bool> DeleteKnownUidAsync(
        int uidId,
        CancellationToken cancellationToken);
}
