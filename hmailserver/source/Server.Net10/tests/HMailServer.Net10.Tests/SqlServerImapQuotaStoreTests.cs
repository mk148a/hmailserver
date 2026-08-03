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
}
