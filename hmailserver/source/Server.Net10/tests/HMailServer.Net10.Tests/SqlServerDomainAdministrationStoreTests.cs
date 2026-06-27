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
        StringAssert.Contains(sql, "domainpostmaster");
        StringAssert.Contains(sql, "domainmaxmessagesize");
        StringAssert.Contains(sql, "domainuseplusaddressing");
        StringAssert.Contains(sql, "domainplusaddressingchar");
        StringAssert.Contains(sql, "domainmaxsize");
        StringAssert.Contains(sql, "domainmaxnoofaccounts");
        StringAssert.Contains(sql, "domainmaxnoofaliases");
        StringAssert.Contains(sql, "domainmaxnoofdistributionlists");
        StringAssert.Contains(sql, "domainlimitationsenabled");
        StringAssert.Contains(sql, "domainmaxaccountsize");
        StringAssert.Contains(sql, "FROM hm_domains");
        StringAssert.Contains(sql, "ORDER BY domainname ASC");
    }
}
