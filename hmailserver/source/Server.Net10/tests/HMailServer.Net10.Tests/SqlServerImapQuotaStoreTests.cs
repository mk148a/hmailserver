using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapQuotaStoreTests
{
    [TestMethod]
    public void QuotaSql_UsesSettingsAccountsDomainsAndLiveMailboxSize()
    {
        StringAssert.Contains(SqlServerImapQuotaStore.SelectQuotaEnabledSql, "enableimapquota");
        StringAssert.Contains(SqlServerImapQuotaStore.SelectQuotaSnapshotSql, "FROM hm_accounts AS a");
        StringAssert.Contains(SqlServerImapQuotaStore.SelectQuotaSnapshotSql, "INNER JOIN hm_domains AS d");
        StringAssert.Contains(SqlServerImapQuotaStore.SelectQuotaSnapshotSql, "LEFT JOIN hm_messages AS m");
        StringAssert.Contains(SqlServerImapQuotaStore.SelectQuotaSnapshotSql, "SUM(m.messagesize)");
        Assert.IsFalse(SqlServerImapQuotaStore.SelectQuotaSnapshotSql.Contains("messagetype", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(SqlServerImapQuotaStore.UpdateAccountQuotaSql, "SET accountmaxsize = @MaxSizeMb");
    }

    [TestMethod]
    public void QuotaSnapshot_DoesNotUseDomainLimitWhenAccountMaxSizeIsZero()
    {
        var quota = CreateQuota(
            accountMaxSizeMb: 0,
            domainLimitationsEnabled: true,
            domainMaxAccountSizeMb: 25,
            usedBytes: 3072);

        Assert.AreEqual(3, quota.UsedKilobytes);
        Assert.IsNull(quota.LimitKilobytes);
    }

    [TestMethod]
    public void QuotaSnapshot_DoesNotUseDomainLimitWhenAccountMaxSizeIsNegative()
    {
        var quota = CreateQuota(
            accountMaxSizeMb: -1,
            domainLimitationsEnabled: true,
            domainMaxAccountSizeMb: 25,
            usedBytes: 3072);

        Assert.AreEqual(3, quota.UsedKilobytes);
        Assert.IsNull(quota.LimitKilobytes);
    }

    [TestMethod]
    public void QuotaSnapshot_UsesPositiveAccountMaxSizeInKilobytes()
    {
        var quota = CreateQuota(
            accountMaxSizeMb: 25,
            domainLimitationsEnabled: false,
            domainMaxAccountSizeMb: 0,
            usedBytes: 3072);

        Assert.AreEqual(3, quota.UsedKilobytes);
        Assert.AreEqual(25 * 1024L, quota.LimitKilobytes);
    }

    private static ImapQuota CreateQuota(
        int accountMaxSizeMb,
        bool domainLimitationsEnabled,
        int domainMaxAccountSizeMb,
        long usedBytes)
    {
        var snapshotType = typeof(SqlServerImapQuotaStore).GetNestedType(
            "QuotaSnapshot",
            System.Reflection.BindingFlags.NonPublic)!;
        var snapshot = Activator.CreateInstance(
            snapshotType,
            accountMaxSizeMb,
            (byte)0,
            domainLimitationsEnabled,
            domainMaxAccountSizeMb,
            usedBytes)!;
        return (ImapQuota)snapshotType.GetMethod(
            "ToQuota",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)!
            .Invoke(snapshot, new object?[] { string.Empty })!;
    }
}
