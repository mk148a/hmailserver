using HMailServer.Storage.SqlServer;
using HMailServer.Core.Abstractions;

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

    [TestMethod]
    public void InsertDistributionListSql_UsesAllLegacyFieldsAndGeneratedIdentity()
    {
        var sql = SqlServerDistributionListAdministrationStore.InsertDistributionListSql;

        StringAssert.Contains(sql, "INSERT INTO hm_distributionlists");
        StringAssert.Contains(sql, "distributionlistdomainid");
        StringAssert.Contains(sql, "distributionlistenabled");
        StringAssert.Contains(sql, "distributionlistaddress");
        StringAssert.Contains(sql, "distributionlistrequireauth");
        StringAssert.Contains(sql, "distributionlistrequireaddress");
        StringAssert.Contains(sql, "distributionlistmode");
        StringAssert.Contains(sql, "OUTPUT INSERTED.distributionlistid");
        StringAssert.Contains(sql, "@DomainID");
        StringAssert.Contains(sql, "@Active");
        StringAssert.Contains(sql, "@Address");
        StringAssert.Contains(sql, "@RequireSMTPAuth");
        StringAssert.Contains(sql, "@RequireSenderAddress");
        StringAssert.Contains(sql, "@Mode");
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_distributionlistsrecipients", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertDistributionListAsync_ExposesSnapshotAndCancellationContract()
    {
        var method = typeof(SqlServerDistributionListAdministrationStore).GetMethod(
            nameof(SqlServerDistributionListAdministrationStore.InsertDistributionListAsync));

        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(ValueTask<int>), method.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(DistributionListAdministrationSnapshot), typeof(CancellationToken) },
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }
}
