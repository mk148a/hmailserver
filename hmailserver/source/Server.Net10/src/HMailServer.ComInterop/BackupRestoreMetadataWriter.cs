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
    int RestoredRuleActions = 0,
    int RestoredFolders = 0,
    int RestoredMessages = 0,
    int RestoredSecurityRanges = 0,
    int RestoredTcpIpPorts = 0,
    int RestoredBlockedAttachments = 0,
    int RestoredSurblServers = 0,
    int RestoredDnsBlackLists = 0);

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

    public static async ValueTask<BackupRestoreMetadataResult> RestoreSecurityRangesAsync(
        IReadOnlyList<SecurityRangeAdministrationSnapshot> ranges,
        ISecurityRangeAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var range in ranges)
                {
                    var insertedId = await store.InsertSecurityRangeAsync(range, ct).ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;
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
            RestoredSecurityRanges: restored);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreTcpIpPortsAsync(
        IReadOnlyList<RestoreTcpIpPortEntry> ports,
        ITcpIpPortAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var entry in ports)
                {
                    var insertedId = await store.InsertTcpIpPortForRestoreAsync(entry.Port, ct)
                        .ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;
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
            RestoredTcpIpPorts: restored);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreBlockedAttachmentsAsync(
        IReadOnlyList<RestoreBlockedAttachmentEntry> attachments,
        IBlockedAttachmentAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var entry in attachments)
                {
                    var insertedId = await store.InsertBlockedAttachmentForRestoreAsync(entry.Attachment, ct)
                        .ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new BackupRestoreMetadataResult(RestoredDomains: 0, RestoredAccounts: 0,
            RestoredAliases: 0, RestoredDistributionLists: 0, RestoredRecipients: 0,
            RestoredTcpIpPorts: 0,
            RestoredBlockedAttachments: restored);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreSurblServersAsync(
        IReadOnlyList<RestoreSurblServerEntry> servers,
        ISurblServerAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
    {
        ArgumentNullException.ThrowIfNull(servers);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var entry in servers)
                {
                    var insertedId = await store.InsertSurblServerForRestoreAsync(entry.Server, ct)
                        .ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;
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
            RestoredSurblServers: restored);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreDnsBlackListsAsync(
        IReadOnlyList<RestoreDnsBlackListEntry> blackLists,
        IDnsBlackListAdministrationStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onInserted = null)
    {
        ArgumentNullException.ThrowIfNull(blackLists);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var entry in blackLists)
                {
                    var insertedId = await store.InsertDnsBlackListForRestoreAsync(entry.BlackList, ct)
                        .ConfigureAwait(false);
                    onInserted?.Invoke(insertedId);
                    restored++;
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
            RestoredDnsBlackLists: restored);
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

    public static async ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> RestoreGroupsAsync(
        IReadOnlyList<RestoreGroupEntry> groups,
        IReadOnlyList<AccountAdministrationSnapshot> accounts,
        IGroupAdministrationStore groupStore,
        IGroupMemberAdministrationStore memberStore,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(groupStore);
        ArgumentNullException.ThrowIfNull(memberStore);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = groups
            .Select(entry =>
            {
                if (!names.Add(entry.Group.Name))
                {
                    throw new InvalidDataException($"The restore contains duplicate group '{entry.Group.Name}'.");
                }

                var members = entry.MemberNames
                    .Select(address =>
                    {
                        var matches = accounts
                            .Where(account => string.Equals(account.Address, address, StringComparison.OrdinalIgnoreCase))
                            .ToArray();
                        if (matches.Length != 1 || matches[0].Id <= 0)
                        {
                            throw new InvalidDataException(
                                $"The group member account '{address}' could not be resolved uniquely.");
                        }

                        return matches[0].Id;
                    })
                    .ToArray();
                return (entry.Group, members);
            })
            .ToArray();

        var restored = new List<GroupAdministrationSnapshot>(resolved.Length);
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var item in resolved)
                {
                    var groupId = await groupStore.InsertGroupAsync(item.Group, ct).ConfigureAwait(false);
                    if (groupId <= 0)
                    {
                        throw new InvalidOperationException("The group restore did not return a persisted ID.");
                    }

                    var restoredGroup = item.Group with { Id = groupId };
                    restored.Add(restoredGroup);
                    foreach (var accountId in item.members)
                    {
                        var memberId = await memberStore.InsertGroupMemberAsync(
                            new GroupMemberAdministrationSnapshot(0, groupId, accountId),
                            ct).ConfigureAwait(false);
                        if (memberId <= 0)
                        {
                            throw new InvalidOperationException("The group-member restore did not return a persisted ID.");
                        }
                    }
                }
            },
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken).ConfigureAwait(false);

        return restored;
    }

    public static async ValueTask<int> RestorePublicFolderPermissionsAsync(
        IReadOnlyList<RestoreFolderPermissionEntry> permissions,
        int folderId,
        IReadOnlyList<AccountAdministrationSnapshot> accounts,
        IReadOnlyList<GroupAdministrationSnapshot> groups,
        IImapFolderPermissionAdministrationRestoreStore store,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);
        if (folderId <= 0)
        {
            throw new InvalidDataException("The public-folder ACL restore requires a persisted folder ID.");
        }

        // Resolve every holder before the first insert so an invalid archive cannot
        // leave a partially written ACL batch outside the caller's transaction.
        var resolved = permissions
            .Select(permission => PublicFolderAclHolderResolver.Resolve(permission, accounts, groups))
            .ToArray();
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: ct => RestorePublicFolderPermissionsCoreAsync(folderId, resolved, store, ct),
            commitAsync: _ => default,
            rollbackAsync: rollbackAsync,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return resolved.Length;
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestorePublicFoldersAsync(
        IReadOnlyList<RestorePublicFolderEntry> folders,
        IReadOnlyList<AccountAdministrationSnapshot> accounts,
        IReadOnlyList<GroupAdministrationSnapshot> groups,
        IImapFolderAdministrationRestoreStore folderStore,
        IMessageAdministrationRestoreStore messageStore,
        IImapFolderPermissionAdministrationRestoreStore permissionStore,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(folderStore);
        ArgumentNullException.ThrowIfNull(messageStore);
        ArgumentNullException.ThrowIfNull(permissionStore);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restoredFolders = 0;
        var restoredMessages = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var root in folders)
                {
                    await RestorePublicFolderAsync(
                        root,
                        parentFolderId: -1,
                        accounts,
                        groups,
                        folderStore,
                        messageStore,
                        permissionStore,
                        ct).ConfigureAwait(false);
                    restoredFolders += CountPublicFolders(root);
                    restoredMessages += CountPublicMessages(root);
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
            RestoredFolders: restoredFolders,
            RestoredMessages: restoredMessages);
    }

    public static async ValueTask<BackupRestoreMetadataResult> RestoreFoldersAsync(
        IReadOnlyList<RestoreFolderEntry> folders,
        int accountId,
        IImapFolderAdministrationRestoreStore store,
        IMessageAdministrationRestoreStore? messageStore,
        Func<ValueTask> rollbackAsync,
        CancellationToken cancellationToken,
        Action<int>? onRootInserted = null,
        Action<int, long>? onMessageInserted = null)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rollbackAsync);

        var restored = 0;
        await BackupRestoreTransactionBoundary.ExecuteAsync(
            mutateAsync: async ct =>
            {
                foreach (var root in folders)
                {
                    await RestoreFolderAsync(
                        root,
                        accountId,
                        parentFolderId: -1,
                        store,
                        messageStore,
                        ct,
                        onRootInserted,
                        onMessageInserted).ConfigureAwait(false);
                    restored += CountFolders(root);
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
            RestoredFolders: restored,
            RestoredMessages: folders.Sum(static folder => CountMessages(folder)));
    }

    private static async ValueTask<int> RestoreFolderAsync(
        RestoreFolderEntry entry,
        int accountId,
        int parentFolderId,
        IImapFolderAdministrationRestoreStore store,
        IMessageAdministrationRestoreStore? messageStore,
        CancellationToken cancellationToken,
        Action<int>? onRootInserted,
        Action<int, long>? onMessageInserted)
    {
        var inserted = await store.InsertFolderForRestoreAsync(
            entry.Folder with { AccountId = accountId, ParentId = parentFolderId },
            cancellationToken).ConfigureAwait(false);
        if (parentFolderId == -1)
        {
            onRootInserted?.Invoke(inserted.Id);
        }
        foreach (var message in entry.Messages)
        {
            if (messageStore is null)
            {
                throw new InvalidOperationException("Message restore requires a message administration restore store.");
            }
            var insertedMessage = await messageStore.InsertMessageForRestoreAsync(
                accountId,
                inserted.Id,
                message with { AccountId = accountId, FolderId = inserted.Id },
                cancellationToken).ConfigureAwait(false);
            onMessageInserted?.Invoke(inserted.Id, insertedMessage.MessageId);
        }
        foreach (var child in entry.Children)
        {
            await RestoreFolderAsync(child, accountId, inserted.Id, store, messageStore, cancellationToken, onRootInserted, onMessageInserted)
                .ConfigureAwait(false);
        }

        return inserted.Id;
    }

    private static async ValueTask RestorePublicFolderAsync(
        RestorePublicFolderEntry entry,
        int parentFolderId,
        IReadOnlyList<AccountAdministrationSnapshot> accounts,
        IReadOnlyList<GroupAdministrationSnapshot> groups,
        IImapFolderAdministrationRestoreStore folderStore,
        IMessageAdministrationRestoreStore messageStore,
        IImapFolderPermissionAdministrationRestoreStore permissionStore,
        CancellationToken cancellationToken)
    {
        var inserted = await folderStore.InsertFolderForRestoreAsync(
            entry.Folder with { AccountId = 0, ParentId = parentFolderId },
            cancellationToken).ConfigureAwait(false);

        foreach (var message in entry.Messages)
        {
            await messageStore.InsertMessageForRestoreAsync(
                accountId: 0,
                folderId: inserted.Id,
                message with { AccountId = 0, FolderId = inserted.Id },
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var child in entry.Children)
        {
            await RestorePublicFolderAsync(
                child,
                inserted.Id,
                accounts,
                groups,
                folderStore,
                messageStore,
                permissionStore,
                cancellationToken).ConfigureAwait(false);
        }

        var resolved = entry.Permissions
            .Select(permission => PublicFolderAclHolderResolver.Resolve(permission, accounts, groups))
            .ToArray();
        await RestorePublicFolderPermissionsCoreAsync(
            inserted.Id,
            resolved,
            permissionStore,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask RestorePublicFolderPermissionsCoreAsync(
        int folderId,
        IReadOnlyList<ResolvedPublicFolderAclPermission> permissions,
        IImapFolderPermissionAdministrationRestoreStore store,
        CancellationToken cancellationToken)
    {
        foreach (var permission in permissions)
        {
            var inserted = await store.InsertFolderPermissionForRestoreAsync(
                folderId,
                permission.PermissionType,
                permission.PermissionGroupId,
                permission.PermissionAccountId,
                permission.Rights,
                cancellationToken).ConfigureAwait(false);
            if (inserted is null)
            {
                throw new InvalidOperationException(
                    "The public-folder ACL restore did not insert a permission row.");
            }
        }
    }

    private static int CountFolders(RestoreFolderEntry entry) =>
        1 + entry.Children.Sum(CountFolders);

    private static int CountMessages(RestoreFolderEntry entry) =>
        entry.Messages.Count + entry.Children.Sum(CountMessages);

    private static int CountPublicFolders(RestorePublicFolderEntry entry) =>
        1 + entry.Children.Sum(CountPublicFolders);

    private static int CountPublicMessages(RestorePublicFolderEntry entry) =>
        entry.Messages.Count + entry.Children.Sum(CountPublicMessages);
}
