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
                typeof(IAccountAdministrationStore),
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
            new BackupStartPlanEvidence("unused", BackupStartPlan.BackupDomainsFlag, false, true, true),
            CancellationToken.None);

        Assert.AreEqual(1, factory.BeginCount);
        Assert.AreEqual(1, factory.DisposeCount);
        Assert.AreEqual(1, payload.Domains!.Count);
        Assert.AreEqual("snapshot.example", payload.Domains[0].Name);
    }

    private sealed class RecordingSnapshotFactory : IBackupDomainProjectionSnapshotFactory
    {
        public int BeginCount { get; private set; }
        public int DisposeCount { get; private set; }

        public ValueTask<IBackupDomainProjectionSnapshot> BeginAsync(CancellationToken cancellationToken)
        {
            BeginCount++;
            return ValueTask.FromResult<IBackupDomainProjectionSnapshot>(
                new RecordingSnapshot(this));
        }

        private sealed class RecordingSnapshot(RecordingSnapshotFactory owner)
            : IBackupDomainProjectionSnapshot
        {
            public IDomainAdministrationStore DomainStore { get; } =
                new FixedDomainStore(new DomainAdministrationSnapshot(7, "snapshot.example", true));
            public IAccountAdministrationStore AccountStore { get; } = new EmptyAccountStore();
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

    private sealed class EmptyAliasStore : IAliasAdministrationStore
    {
        public ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(int domainId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AliasAdministrationSnapshot>>(Array.Empty<AliasAdministrationSnapshot>());
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
