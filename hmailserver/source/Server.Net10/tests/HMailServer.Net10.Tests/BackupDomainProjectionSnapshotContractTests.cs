using System.Reflection;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupDomainProjectionSnapshotContractTests
{
    [TestMethod]
    public void SnapshotContract_IsReadOnlyAndContainsOnlyDomainProjectionStores()
    {
        var members = typeof(IBackupDomainProjectionSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(static property => property.PropertyType)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                typeof(IDomainAdministrationStore),
                typeof(ISettingsAdministrationStore),
                typeof(IBackupSettingsPropertyStore),
                typeof(IGroupAdministrationStore),
                typeof(IGroupMemberAdministrationStore),
                typeof(IAccountAdministrationStore),
                typeof(IBackupAccountAdministrationStore),
                typeof(IBackupFetchAccountAdministrationStore),
                typeof(IBackupRuleAdministrationStore),
                typeof(IRuleCriteriaAdministrationStore),
                typeof(IRuleActionAdministrationStore),
                typeof(IImapFolderAdministrationStore),
                typeof(IMessageAdministrationBackupStore),
                typeof(IDomainAliasAdministrationStore),
                typeof(IAliasAdministrationStore),
                typeof(IDistributionListAdministrationStore),
                typeof(IDistributionListRecipientAdministrationStore)
            },
            members);

        Assert.IsTrue(typeof(IBackupDomainProjectionSnapshot).IsAssignableTo(typeof(IAsyncDisposable)));
        Assert.IsFalse(
            typeof(IBackupDomainProjectionSnapshot)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(static method => method.Name.Contains("Insert", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SnapshotFactoryRequiresAConnectionFactory()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new SqlServerBackupDomainProjectionSnapshotFactory(null!));
    }

    [TestMethod]
    public async Task DomainOnlyPayloadUsesOneProjectionSnapshotAndDisposesIt()
    {
        var factory = new RecordingSnapshotFactory();
        var runtime = new BackupXmlPayloadRuntime(
            new EmptySettingsStore(),
            new ThrowingDomainStore(),
            new EmptyDomainAliasStore(),
            new EmptyAccountStore(),
            new EmptyAliasStore(),
            new EmptyDistributionListStore(),
            new EmptyRecipientStore(),
            domainProjectionSnapshotFactory: factory);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                "unused",
                BackupStartPlan.BackupDomainsFlag | BackupStartPlan.BackupMessagesFlag,
                true,
                true,
                true),
            CancellationToken.None);

        Assert.AreEqual(1, factory.BeginCount);
        Assert.AreEqual(1, factory.DisposeCount);
        Assert.AreEqual(1, payload.Domains!.Count);
        Assert.AreEqual("snapshot.example", payload.Domains[0].Name);
        Assert.AreEqual("encrypted", payload.BackupAccounts![7][0].Password);
        Assert.AreEqual(2, payload.BackupAccounts[7][0].PasswordEncryption);
        Assert.AreEqual("fetch-encrypted", payload.BackupFetchAccounts![9][0].Password);
        Assert.AreEqual("rule", payload.Rules![9][0].Name);
        Assert.AreEqual("match", payload.RuleCriterias![101][0].MatchValue);
        Assert.AreEqual("subject", payload.RuleActions![101][0].Subject);
        Assert.IsNotNull(payload.Folders);
        Assert.IsNotNull(payload.FolderMessages);
    }

    [TestMethod]
    public async Task SettingsAndDomainsPayloadUsesTheSameProjectionSnapshot()
    {
        var factory = new RecordingSnapshotFactory(
            new FixedSettingsStore(new SettingsAdministrationSnapshot(
                "snapshot-host",
                "smtp",
                "pop3",
                "imap")),
            new FixedBackupSettingsPropertyStore(
                new BackupSettingsPropertySnapshot("defaultdomain", 0, "snapshot.example")));
        var runtime = new BackupXmlPayloadRuntime(
            new EmptySettingsStore(),
            new ThrowingDomainStore(),
            new EmptyDomainAliasStore(),
            new EmptyAccountStore(),
            new EmptyAliasStore(),
            new EmptyDistributionListStore(),
            new EmptyRecipientStore(),
            domainProjectionSnapshotFactory: factory);

        var payload = await runtime.GetPayloadAsync(
            new BackupStartPlanEvidence(
                "unused",
                BackupStartPlan.BackupSettingsFlag | BackupStartPlan.BackupDomainsFlag,
                false,
                true,
                true),
            CancellationToken.None);

        Assert.AreEqual(1, factory.BeginCount);
        Assert.AreEqual(1, factory.DisposeCount);
        Assert.AreEqual("snapshot-host", payload.Settings!.HostName);
        Assert.AreEqual("defaultdomain", payload.SettingsProperties![0].Name);
        Assert.AreEqual("snapshot.example", payload.SettingsProperties[0].StringValue);
    }

    private sealed class RecordingSnapshotFactory(
        ISettingsAdministrationStore? settingsStore = null,
        IBackupSettingsPropertyStore? backupSettingsPropertyStore = null)
        : IBackupDomainProjectionSnapshotFactory
    {
        public int BeginCount { get; private set; }
        public int DisposeCount { get; private set; }

        public ValueTask<IBackupDomainProjectionSnapshot> BeginAsync(CancellationToken cancellationToken)
        {
            BeginCount++;
            return ValueTask.FromResult<IBackupDomainProjectionSnapshot>(
                new RecordingSnapshot(
                    this,
                    settingsStore ?? new EmptySettingsStore(),
                    backupSettingsPropertyStore ?? new EmptyBackupSettingsPropertyStore()));
        }

        private sealed class RecordingSnapshot(
            RecordingSnapshotFactory owner,
            ISettingsAdministrationStore settingsStore,
            IBackupSettingsPropertyStore backupSettingsPropertyStore)
            : IBackupDomainProjectionSnapshot
        {
            public IDomainAdministrationStore DomainStore { get; } =
                new FixedDomainStore(new DomainAdministrationSnapshot(7, "snapshot.example", true));
            public ISettingsAdministrationStore SettingsStore { get; } = settingsStore;
            public IBackupSettingsPropertyStore BackupSettingsPropertyStore { get; } = backupSettingsPropertyStore;
            public IGroupAdministrationStore GroupStore { get; } = new EmptyGroupStore();
            public IGroupMemberAdministrationStore GroupMemberStore { get; } = new EmptyGroupMemberStore();
            public IAccountAdministrationStore AccountStore { get; } = new EmptyAccountStore();
            public IBackupAccountAdministrationStore BackupAccountStore { get; } =
                new FixedBackupAccountStore();
            public IBackupFetchAccountAdministrationStore BackupFetchAccountStore { get; } =
                new FixedBackupFetchAccountStore();
            public IBackupRuleAdministrationStore BackupRuleStore { get; } =
                new FixedBackupRuleStore();
            public IRuleCriteriaAdministrationStore RuleCriteriaStore { get; } =
                new FixedRuleCriteriaStore();
            public IRuleActionAdministrationStore RuleActionStore { get; } =
                new FixedRuleActionStore();
            public IImapFolderAdministrationStore FolderStore { get; } = new EmptyFolderStore();
            public IMessageAdministrationBackupStore MessageBackupStore { get; } = new EmptyMessageBackupStore();
            public IDomainAliasAdministrationStore DomainAliasStore { get; } = new EmptyDomainAliasStore();
            public IAliasAdministrationStore AliasStore { get; } = new EmptyAliasStore();
            public IDistributionListAdministrationStore DistributionListStore { get; } = new EmptyDistributionListStore();
            public IDistributionListRecipientAdministrationStore RecipientStore { get; } = new EmptyRecipientStore();

            public ValueTask DisposeAsync()
            {
                owner.DisposeCount++;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class EmptySettingsStore : ISettingsAdministrationStore
    {
        public ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(CancellationToken cancellationToken) =>
            throw new AssertFailedException("The domain-only snapshot path must not read settings.");
    }

    private sealed class EmptyBackupSettingsPropertyStore : IBackupSettingsPropertyStore
    {
        public ValueTask<IReadOnlyList<BackupSettingsPropertySnapshot>>
            GetBackupSettingsPropertiesAsync(CancellationToken cancellationToken) =>
            throw new AssertFailedException("The domain-only snapshot path must not read settings.");
    }

    private sealed class FixedSettingsStore(SettingsAdministrationSnapshot snapshot)
        : ISettingsAdministrationStore
    {
        public ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(
            CancellationToken cancellationToken) => ValueTask.FromResult(snapshot);
    }

    private sealed class FixedBackupSettingsPropertyStore(BackupSettingsPropertySnapshot property)
        : IBackupSettingsPropertyStore
    {
        public ValueTask<IReadOnlyList<BackupSettingsPropertySnapshot>>
            GetBackupSettingsPropertiesAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<BackupSettingsPropertySnapshot>>(new[] { property });
    }

    private sealed class EmptyGroupStore : IGroupAdministrationStore
    {
        public ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> GetGroupsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<GroupAdministrationSnapshot>>(
                Array.Empty<GroupAdministrationSnapshot>());
    }

    private sealed class EmptyGroupMemberStore : IGroupMemberAdministrationStore
    {
        public ValueTask<IReadOnlyList<GroupMemberAdministrationSnapshot>> GetGroupMembersAsync(
            int groupId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<GroupMemberAdministrationSnapshot>>(
                Array.Empty<GroupMemberAdministrationSnapshot>());
    }

    private sealed class ThrowingDomainStore : IDomainAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(CancellationToken cancellationToken) =>
            throw new AssertFailedException("The domain-only snapshot path must not read the ordinary domain store.");
    }

    private sealed class FixedDomainStore(DomainAdministrationSnapshot domain) : IDomainAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DomainAdministrationSnapshot>>(new[] { domain });
    }

    private sealed class EmptyDomainAliasStore : IDomainAliasAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAliasAdministrationSnapshot>> GetDomainAliasesAsync(int domainId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DomainAliasAdministrationSnapshot>>(Array.Empty<DomainAliasAdministrationSnapshot>());
    }

    private sealed class EmptyAccountStore : IAccountAdministrationStore
    {
        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(int domainId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AccountAdministrationSnapshot>>(Array.Empty<AccountAdministrationSnapshot>());

        public ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(int accountId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<AccountAdministrationSnapshot?>(null);
    }

    private sealed class FixedBackupAccountStore : IBackupAccountAdministrationStore
    {
        public ValueTask<IReadOnlyList<AccountBackupAdministrationSnapshot>> GetBackupAccountsAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AccountBackupAdministrationSnapshot>>(
                new[]
                {
                    new AccountBackupAdministrationSnapshot(
                        new AccountAdministrationSnapshot(9, domainId, "user@snapshot.example", true, 0),
                        "encrypted",
                        2)
                });
    }

    private sealed class FixedBackupFetchAccountStore : IBackupFetchAccountAdministrationStore
    {
        public ValueTask<IReadOnlyList<FetchAccountBackupAdministrationSnapshot>> GetBackupFetchAccountsAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<FetchAccountBackupAdministrationSnapshot>>(
                new[]
                {
                    new FetchAccountBackupAdministrationSnapshot(
                        new FetchAccountAdministrationSnapshot(
                            12,
                            accountId,
                            "fetch",
                            "imap.example",
                            993,
                            1,
                            "user",
                            5,
                            7,
                            true,
                            false,
                            false,
                            2,
                            false,
                            false,
                            false,
                            "",
                            "",
                            false),
                        "fetch-encrypted",
                        new[]
                        {
                            new FetchAccountUidBackupAdministrationSnapshot(
                                "uid",
                                "2026-08-25 00:00:00")
                        })
                });
    }

    private sealed class FixedBackupRuleStore : IBackupRuleAdministrationStore
    {
        public ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetBackupRulesAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<RuleAdministrationSnapshot>>(
                new[] { new RuleAdministrationSnapshot(101, accountId, "rule", true, true, 1) });
    }

    private sealed class FixedRuleCriteriaStore : IRuleCriteriaAdministrationStore
    {
        public ValueTask<IReadOnlyList<RuleCriteriaAdministrationSnapshot>> GetRuleCriteriaAsync(
            int ruleId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<RuleCriteriaAdministrationSnapshot>>(
                new[] { new RuleCriteriaAdministrationSnapshot(201, ruleId, "match", true, 1, 2, "") });

        public ValueTask DeleteRuleCriteriaByIdAsync(int ruleId, int databaseId, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask SaveRuleCriteriaAsync(int owningRuleId, RuleCriteriaAdministrationSnapshot criterion, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    private sealed class FixedRuleActionStore : IRuleActionAdministrationStore
    {
        public ValueTask<IReadOnlyList<RuleActionAdministrationSnapshot>> GetRuleActionsAsync(
            int ruleId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<RuleActionAdministrationSnapshot>>(
                new[]
                {
                    new RuleActionAdministrationSnapshot(
                        301,
                        ruleId,
                        1,
                        "subject",
                        "body",
                        "",
                        "",
                        "",
                        "to@example.test",
                        "",
                        "",
                        "",
                        "",
                        0,
                        false,
                        1)
                });

        public ValueTask DeleteRuleActionByIdAsync(int ruleId, int databaseId, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask SaveRuleActionAsync(int owningRuleId, RuleActionAdministrationSnapshot action, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    private sealed class EmptyAliasStore : IAliasAdministrationStore
    {
        public ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(int domainId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AliasAdministrationSnapshot>>(Array.Empty<AliasAdministrationSnapshot>());
    }

    private sealed class EmptyFolderStore : IImapFolderAdministrationStore
    {
        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetFoldersForAccountAsync(int accountId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(Array.Empty<ImapFolderAdministrationSnapshot>());

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(int accountId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(Array.Empty<ImapFolderAdministrationSnapshot>());

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetChildFoldersAsync(int parentFolderId, int accountId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(Array.Empty<ImapFolderAdministrationSnapshot>());

        public ValueTask<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>> GetFolderPermissionsAsync(int folderId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>>(Array.Empty<ImapFolderPermissionAdministrationSnapshot>());
    }

    private sealed class EmptyMessageBackupStore : IMessageAdministrationBackupStore
    {
        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesForBackupAsync(int accountId, int folderId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(Array.Empty<MessageAdministrationSnapshot>());
    }

    private sealed class EmptyDistributionListStore : IDistributionListAdministrationStore
    {
        public ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(int domainId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DistributionListAdministrationSnapshot>>(Array.Empty<DistributionListAdministrationSnapshot>());
    }

    private sealed class EmptyRecipientStore : IDistributionListRecipientAdministrationStore
    {
        public ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(int distributionListId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>(Array.Empty<DistributionListRecipientAdministrationSnapshot>());
    }
}
