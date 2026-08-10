using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed record BackupRestoreMetadataResult(
    int RestoredDomains,
    int RestoredAccounts,
    int RestoredAliases,
    int RestoredDistributionLists,
    int RestoredRecipients,
    int RestoredFetchAccounts = 0,
    int RestoredFetchAccountUids = 0,
    int RestoredRules = 0,
    int RestoredRuleCriteria = 0,
    int RestoredRuleActions = 0);

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
                    var insertedId = await store.InsertAccountForRestoreAsync(
                        domainId,
                        entry.Account,
                        entry.Password,
                        entry.PasswordEncryption,
                        ct).ConfigureAwait(false);
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

    public static async ValueTask<BackupRestoreMetadataResult> RestoreFetchAccountsAsync(
        IReadOnlyList<RestoreFetchAccountEntry> fetchAccounts,
        int accountId,
        IFetchAccountAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
    {
        ArgumentNullException.ThrowIfNull(fetchAccounts);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        var restoredUids = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var entry in fetchAccounts)
                {
                    var insertedId = await store.InsertFetchAccountForRestoreAsync(
                        entry.Account with { AccountId = accountId },
                        entry.EncryptedPassword,
                        ct).ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;

                    foreach (var uid in entry.Uids)
                    {
                        await store.InsertFetchAccountUidAsync(insertedId, uid.Value, uid.Date, ct).ConfigureAwait(false);
                        restoredUids++;
                    }
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken);

        return new BackupRestoreMetadataResult(
            RestoredDomains: 0,
            RestoredAccounts: 0,
            RestoredAliases: 0,
            RestoredDistributionLists: 0,
            RestoredRecipients: 0,
            RestoredFetchAccounts: restored,
            RestoredFetchAccountUids: restoredUids);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreRulesAsync(
        IReadOnlyList<RestoreRuleEntry> rules,
        int accountId,
        IRuleAdministrationStore ruleStore,
        IRuleCriteriaAdministrationStore criteriaStore,
        IRuleActionAdministrationStore actionStore,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ruleStore);
        ArgumentNullException.ThrowIfNull(criteriaStore);
        ArgumentNullException.ThrowIfNull(actionStore);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restoredRules = 0;
        var restoredCriteria = 0;
        var restoredActions = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var entry in rules)
                {
                    var ruleId = await ruleStore.InsertRuleAsync(
                        accountId,
                        entry.Rule with { AccountId = accountId },
                        ct).ConfigureAwait(false);
                    onInserted?.Invoke(ruleId);
                    restoredRules++;

                    foreach (var criterion in entry.Criteria)
                    {
                        await criteriaStore.InsertRuleCriteriaAsync(
                            ruleId,
                            criterion with { RuleId = ruleId },
                            ct).ConfigureAwait(false);
                        restoredCriteria++;
                    }

                    foreach (var action in entry.Actions)
                    {
                        await actionStore.InsertRuleActionAsync(
                            ruleId,
                            action with { RuleId = ruleId },
                            ct).ConfigureAwait(false);
                        restoredActions++;
                    }
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new BackupRestoreMetadataResult(
            RestoredDomains: 0,
            RestoredAccounts: 0,
            RestoredAliases: 0,
            RestoredDistributionLists: 0,
            RestoredRecipients: 0,
            RestoredRules: restoredRules,
            RestoredRuleCriteria: restoredCriteria,
            RestoredRuleActions: restoredActions);
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
