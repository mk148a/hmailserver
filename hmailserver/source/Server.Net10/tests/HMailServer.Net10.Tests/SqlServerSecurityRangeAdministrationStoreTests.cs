using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSecurityRangeAdministrationStoreTests
{
    [TestMethod]
    public void GetSecurityRangesSql_UsesLegacySecurityRangeTableColumnsAndOrdering()
    {
        var sql = SqlServerSecurityRangeAdministrationStore.GetSecurityRangesSql;

        StringAssert.Contains(sql, "FROM hm_securityranges");
        StringAssert.Contains(sql, "rangeid");
        StringAssert.Contains(sql, "rangename");
        StringAssert.Contains(sql, "rangepriorityid");
        StringAssert.Contains(sql, "rangelowerip1");
        StringAssert.Contains(sql, "rangelowerip2");
        StringAssert.Contains(sql, "rangeupperip1");
        StringAssert.Contains(sql, "rangeupperip2");
        StringAssert.Contains(sql, "rangeoptions");
        StringAssert.Contains(sql, "rangeexpires");
        StringAssert.Contains(sql, "rangeexpirestime");
        StringAssert.Contains(sql, "ORDER BY rangeexpires ASC, rangepriorityid DESC, rangename ASC");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertSecurityRangeSql_UsesIdentityInsertWithAllLegacyColumns()
    {
        var sql = SqlServerSecurityRangeAdministrationStore.InsertSecurityRangeSql;

        StringAssert.Contains(sql, "INSERT INTO hm_securityranges");
        StringAssert.Contains(sql, "rangename");
        StringAssert.Contains(sql, "rangepriorityid");
        StringAssert.Contains(sql, "rangelowerip1");
        StringAssert.Contains(sql, "rangelowerip2");
        StringAssert.Contains(sql, "rangeupperip1");
        StringAssert.Contains(sql, "rangeupperip2");
        StringAssert.Contains(sql, "rangeoptions");
        StringAssert.Contains(sql, "rangeexpires");
        StringAssert.Contains(sql, "rangeexpirestime");
        StringAssert.Contains(sql, "OUTPUT INSERTED.rangeid");
        StringAssert.Contains(sql, "@lowerIp2");
        StringAssert.Contains(sql, "@upperIp2");
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }
}
