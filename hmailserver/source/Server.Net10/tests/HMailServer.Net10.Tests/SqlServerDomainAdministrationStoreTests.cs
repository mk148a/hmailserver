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
        StringAssert.Contains(sql, "domainantispamoptions");
        StringAssert.Contains(sql, "domaindkimselector");
        StringAssert.Contains(sql, "domaindkimprivatekeyfile");
        StringAssert.Contains(sql, "FROM hm_domains");
        StringAssert.Contains(sql, "ORDER BY domainname ASC");
    }

    [TestMethod]
    public void GetDomainsSql_KeepsDkimProjectionReadOnlyAndDomainScoped()
    {
        var sql = SqlServerDomainAdministrationStore.GetDomainsSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("OPENROWSET", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("BULK", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(sql, "FROM hm_domains");
        StringAssert.Contains(sql, "domainantispamoptions");
        StringAssert.Contains(sql, "domaindkimselector");
        StringAssert.Contains(sql, "domaindkimprivatekeyfile");
    }
}
