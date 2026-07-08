using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerGroupAdministrationStoreTests
{
    [TestMethod]
    public void GetGroupsSql_UsesLegacyGroupTableColumnsAndNameOrdering()
    {
        var sql = SqlServerGroupAdministrationStore.GetGroupsSql;

        StringAssert.Contains(sql, "FROM hm_groups");
        StringAssert.Contains(sql, "groupid");
        StringAssert.Contains(sql, "groupname");
        StringAssert.Contains(sql, "ORDER BY groupname ASC");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }
}
