using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed record BackupRestoreMetadataResult(int RestoredDomains, int RestoredAccounts);

[ComVisible(false)]
public static class BackupRestoreMetadataWriter
{
    public static async ValueTask<BackupRestoreMetadataResult> RestoreDomainsAsync(
        IReadOnlyList<DomainAdministrationSnapshot> domains,
        IDomainAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var domain in domains)
                {
                    await store.InsertDomainAsync(domain, ct).ConfigureAwait(false);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(restored, RestoredAccounts: 0);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreAccountsAsync(
        IReadOnlyList<RestoreAccountEntry> accounts,
        int domainId,
        IAccountAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var entry in accounts)
                {
                    await store.InsertAccountAsync(domainId, entry.Account, entry.Password, ct).ConfigureAwait(false);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(RestoredDomains: 0, restored);
    }
}