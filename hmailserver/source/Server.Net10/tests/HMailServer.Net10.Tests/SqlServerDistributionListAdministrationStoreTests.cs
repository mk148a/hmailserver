using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDistributionListAdministrationStoreTests
{
    [TestMethod]
    public void GetDistributionListsSql_UsesLegacyDistributionListTableDomainFilterAndAddressOrdering()
    {
        var sql = SqlServerDistributionListAdministrationStore.GetDistributionListsSql;

        StringAssert.Contains(sql, "distributionlistid");
        StringAssert.Contains(sql, "distributionlistdomainid");
        StringAssert.Contains(sql, "distributionlistaddress");
        StringAssert.Contains(sql, "distributionlistenabled");
        StringAssert.Contains(sql, "distributionlistrequireauth");
        StringAssert.Contains(sql, "distributionlistrequireaddress");
        StringAssert.Contains(sql, "distributionlistmode");
        StringAssert.Contains(sql, "FROM hm_distributionlists");
        StringAssert.Contains(sql, "WHERE distributionlistdomainid = @DomainID");
        StringAssert.Contains(sql, "ORDER BY distributionlistaddress ASC");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }
}
