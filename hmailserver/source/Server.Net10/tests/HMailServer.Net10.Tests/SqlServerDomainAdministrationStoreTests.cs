using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDomainAdministrationStoreTests
{
    [TestMethod]
    public void GetDomainsSql_UsesLegacyDomainTableAndNameOrdering()
    {
        var sql = SqlServerDomainAdministrationStore.GetDomainsSql;

        StringAssert.Contains(sql, "domainid");
        StringAssert.Contains(sql, "domainname");
        StringAssert.Contains(sql, "domainactive");
        StringAssert.Contains(sql, "FROM hm_domains");
        StringAssert.Contains(sql, "ORDER BY domainname ASC");
    }
}
