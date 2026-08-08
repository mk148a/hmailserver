using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed record BackupRestoreMetadataResult(int RestoredDomains, int RestoredAccounts, int RestoredAliases, int RestoredDistributionLists, int RestoredRecipients);

[ComVisible(false)]
public static class BackupRestoreMetadataWriter
{
    public static async ValueTask<BackupRestoreMetadataResult> RestoreDomainsAsync(
        IReadOnlyList<DomainAdministrationSnapshot> domains,
        IDomainAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
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
                    var insertedId = await store.InsertDomainAsync(domain, ct).ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(restored, RestoredAccounts: 0, RestoredAliases: 0, RestoredDistributionLists: 0, RestoredRecipients: 0);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreAccountsAsync(
        IReadOnlyList<RestoreAccountEntry> accounts,
        int domainId,
        IAccountAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
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
                    var insertedId = await store.InsertAccountAsync(domainId, entry.Account, entry.Password, ct).ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(RestoredDomains: 0, restored, RestoredAliases: 0, RestoredDistributionLists: 0, RestoredRecipients: 0);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreAliasesAsync(
        IReadOnlyList<AliasAdministrationSnapshot> aliases,
        int domainId,
        IAliasAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
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
                    var insertedId = await store.InsertAliasAsync(domainId, alias, ct).ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(RestoredDomains: 0, RestoredAccounts: 0, restored, RestoredDistributionLists: 0, RestoredRecipients: 0);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreDistributionListsAsync(
        IReadOnlyList<DistributionListAdministrationSnapshot> distributionLists,
        int domainId,
        IDistributionListAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
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
                    var insertedId = await store.InsertDistributionListAsync(distributionList, ct).ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(RestoredDomains: 0, RestoredAccounts: 0, RestoredAliases: 0, restored, RestoredRecipients: 0);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreDistributionListRecipientsAsync(
        IReadOnlyList<DistributionListRecipientAdministrationSnapshot> recipients,
        int distributionListId,
        IDistributionListRecipientAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var recipient in recipients)
                {
                    var scoped = recipient with { ListId = distributionListId };
                    var insertedId = await store.InsertDistributionListRecipientAsync(scoped, ct).ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(RestoredDomains: 0, RestoredAccounts: 0, RestoredAliases: 0, RestoredDistributionLists: 0, restored);
    }
}
