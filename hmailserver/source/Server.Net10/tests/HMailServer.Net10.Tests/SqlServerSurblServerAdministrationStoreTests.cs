using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSurblServerAdministrationStoreTests
{
    [TestMethod]
    public void GetSurblServersSql_UsesLegacyColumnsAndDatabaseIdOrdering()
    {
        var sql = SqlServerSurblServerAdministrationStore.GetSurblServersSql;

        StringAssert.Contains(sql, "FROM hm_surblservers");
        StringAssert.Contains(sql, "surblid");
        StringAssert.Contains(sql, "surblactive");
        StringAssert.Contains(sql, "surblhost");
        StringAssert.Contains(sql, "surblrejectmessage");
        StringAssert.Contains(sql, "surblscore");
        StringAssert.Contains(sql, "ORDER BY surblid ASC");
    }

    [TestMethod]
    public void GetSurblServersSql_RemainsReadOnlyAndDoesNotTouchDnsOrSmtpRuntime()
    {
        var sql = SqlServerSurblServerAdministrationStore.GetSurblServersSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_dnsbl", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("xp_", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("OPENROWSET", StringComparison.OrdinalIgnoreCase));
    }
}
