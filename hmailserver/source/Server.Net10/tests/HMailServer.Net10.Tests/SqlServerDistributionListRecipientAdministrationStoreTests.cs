using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDistributionListRecipientAdministrationStoreTests
{
    [TestMethod]
    public void GetRecipientsSql_UsesLegacyRecipientTableListFilterAndAddressOrdering()
    {
        var sql = SqlServerDistributionListRecipientAdministrationStore.GetRecipientsSql;

        StringAssert.Contains(sql, "distributionlistrecipientid");
        StringAssert.Contains(sql, "distributionlistrecipientlistid");
        StringAssert.Contains(sql, "distributionlistrecipientaddress");
        StringAssert.Contains(sql, "FROM hm_distributionlistsrecipients");
        StringAssert.Contains(sql, "WHERE distributionlistrecipientlistid = @DistributionListID");
        StringAssert.Contains(sql, "ORDER BY distributionlistrecipientaddress ASC");
    }
}
