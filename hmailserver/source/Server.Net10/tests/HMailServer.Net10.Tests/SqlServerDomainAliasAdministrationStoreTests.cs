using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDomainAliasAdministrationStoreTests
{
    [TestMethod]
    public void GetDomainAliasesSql_UsesLegacyDomainAliasTableDomainFilterAndIdOrdering()
    {
        var sql = SqlServerDomainAliasAdministrationStore.GetDomainAliasesSql;

        StringAssert.Contains(sql, "daid");
        StringAssert.Contains(sql, "dadomainid");
        StringAssert.Contains(sql, "daalias");
        StringAssert.Contains(sql, "FROM hm_domain_aliases");
        StringAssert.Contains(sql, "WHERE dadomainid = @DomainID");
        StringAssert.Contains(sql, "ORDER BY daid ASC");
    }
}
