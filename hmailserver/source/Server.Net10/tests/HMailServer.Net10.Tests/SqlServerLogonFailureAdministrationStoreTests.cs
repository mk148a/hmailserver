using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerLogonFailureAdministrationStoreTests
{
    [TestMethod]
    public void ClearLegacyListSql_PreservesLegacyMssqlFutureThreshold()
    {
        var sql = SqlServerLogonFailureAdministrationStore.ClearLegacyListSql;

        StringAssert.Contains(sql, "DELETE FROM hm_logon_failures");
        StringAssert.Contains(sql, "failuretime < DATEADD(minute, 1, GETDATE())");
        Assert.IsFalse(sql.Contains("hm_securityranges", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("EXEC", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SYSUTCDATETIME", StringComparison.OrdinalIgnoreCase));
    }
}
