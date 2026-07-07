using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerAliasAdministrationStoreTests
{
    [TestMethod]
    public void GetAliasesSql_UsesLegacyAliasTableDomainFilterAndNameOrdering()
    {
        var sql = SqlServerAliasAdministrationStore.GetAliasesSql;

        StringAssert.Contains(sql, "aliasid");
        StringAssert.Contains(sql, "aliasdomainid");
        StringAssert.Contains(sql, "aliasname");
        StringAssert.Contains(sql, "aliasvalue");
        StringAssert.Contains(sql, "aliasactive");
        StringAssert.Contains(sql, "FROM hm_aliases");
        StringAssert.Contains(sql, "WHERE aliasdomainid = @DomainID");
        StringAssert.Contains(sql, "ORDER BY aliasname ASC");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }
}
