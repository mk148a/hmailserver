using System.Reflection;
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
}
