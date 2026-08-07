using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed record BackupRestoreMetadataResult(int RestoredDomains, int RestoredAccounts, int RestoredAliases, int RestoredDistributionLists);

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

        return new BackupRestoreMetadataResult(restored, RestoredAccounts: 0, RestoredAliases: 0, RestoredDistributionLists: 0);
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

        return new BackupRestoreMetadataResult(RestoredDomains: 0, restored, RestoredAliases: 0, RestoredDistributionLists: 0);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreAliasesAsync(
        IReadOnlyList<AliasAdministrationSnapshot> aliases,
        int domainId,
        IAliasAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var alias in aliases)
                {
                    await store.InsertAliasAsync(domainId, alias, ct).ConfigureAwait(false);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(RestoredDomains: 0, RestoredAccounts: 0, restored, RestoredDistributionLists: 0);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreDistributionListsAsync(
        IReadOnlyList<DistributionListAdministrationSnapshot> distributionLists,
        int domainId,
        IDistributionListAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(distributionLists);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var distributionList in distributionLists)
                {
                    await store.InsertDistributionListAsync(distributionList, ct).ConfigureAwait(false);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(RestoredDomains: 0, RestoredAccounts: 0, RestoredAliases: 0, restored);
    }
}