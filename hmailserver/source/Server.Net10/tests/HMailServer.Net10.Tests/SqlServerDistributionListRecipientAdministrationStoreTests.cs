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

    [TestMethod]
    public void InsertDistributionListRecipientSql_UsesLegacyFieldsIdentityAndParameters()
    {
        var sql = SqlServerDistributionListRecipientAdministrationStore.InsertDistributionListRecipientSql;

        StringAssert.Contains(sql, "INSERT INTO hm_distributionlistsrecipients");
        StringAssert.Contains(sql, "distributionlistrecipientlistid");
        StringAssert.Contains(sql, "distributionlistrecipientaddress");
        StringAssert.Contains(sql, "OUTPUT INSERTED.distributionlistrecipientid");
        StringAssert.Contains(sql, "VALUES (@ListId, @Address)");
    }

    [TestMethod]
    public void UpdateDistributionListRecipientSql_UsesParameterizedLegacyFieldsIdentityAndOwnerPredicate()
    {
        var sql = SqlServerDistributionListRecipientAdministrationStore.UpdateDistributionListRecipientSql;

        StringAssert.Contains(sql, "UPDATE hm_distributionlistsrecipients");
        StringAssert.Contains(sql, "distributionlistrecipientlistid = @ListId");
        StringAssert.Contains(sql, "distributionlistrecipientaddress = @Address");
        StringAssert.Contains(sql, "WHERE distributionlistrecipientid = @ID");
        StringAssert.Contains(sql, "AND distributionlistrecipientlistid = @ListId");
    }
}
