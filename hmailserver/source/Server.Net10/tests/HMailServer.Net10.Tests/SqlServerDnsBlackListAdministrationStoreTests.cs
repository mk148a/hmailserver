using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDnsBlackListAdministrationStoreTests
{
    [TestMethod]
    public void GetDnsBlackListsSql_UsesLegacyColumnsAndDatabaseIdOrdering()
    {
        var sql = SqlServerDnsBlackListAdministrationStore.GetDnsBlackListsSql;

        StringAssert.Contains(sql, "FROM hm_dnsbl");
        StringAssert.Contains(sql, "sblid");
        StringAssert.Contains(sql, "sblactive");
        StringAssert.Contains(sql, "sbldnshost");
        StringAssert.Contains(sql, "sblrejectmessage");
        StringAssert.Contains(sql, "sblresult");
        StringAssert.Contains(sql, "sblscore");
        StringAssert.Contains(sql, "ORDER BY sblid ASC");
    }

    [TestMethod]
    public void GetDnsBlackListsSql_RemainsReadOnlyAndDoesNotTouchDnsOrSmtpRuntime()
    {
        var sql = SqlServerDnsBlackListAdministrationStore.GetDnsBlackListsSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_greylisting", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("xp_", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("OPENROWSET", StringComparison.OrdinalIgnoreCase));
    }
}
