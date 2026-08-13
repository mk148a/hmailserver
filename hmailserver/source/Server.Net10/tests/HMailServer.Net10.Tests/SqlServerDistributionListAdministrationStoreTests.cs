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

    [TestMethod]
    public void UpdateDistributionListSql_UsesAllLegacyFieldsAndOwnerScopedIdentityPredicate()
    {
        var sql = SqlServerDistributionListAdministrationStore.UpdateDistributionListSql;
        var whereClause = sql[sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)..];

        StringAssert.Contains(sql, "UPDATE hm_distributionlists");
        StringAssert.Contains(sql, "distributionlistdomainid = @DomainID");
        StringAssert.Contains(sql, "distributionlistenabled = @Active");
        StringAssert.Contains(sql, "distributionlistaddress = @Address");
        StringAssert.Contains(sql, "distributionlistrequireauth = @RequireSMTPAuth");
        StringAssert.Contains(sql, "distributionlistrequireaddress = @RequireSenderAddress");
        StringAssert.Contains(sql, "distributionlistmode = @Mode");
        StringAssert.Contains(whereClause, "WHERE distributionlistid = @ID");
        StringAssert.Contains(whereClause, "AND distributionlistdomainid = @DomainID");
        foreach (var parameterName in new[]
                 {
                     "@ID",
                     "@DomainID",
                     "@Active",
                     "@Address",
                     "@RequireSMTPAuth",
                     "@RequireSenderAddress",
                     "@Mode"
                 })
        {
            StringAssert.Contains(sql, parameterName);
        }

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("OUTPUT", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateDistributionListAsync_ExposesSnapshotAndCancellationContract()
    {
        var method = typeof(SqlServerDistributionListAdministrationStore).GetMethod(
            nameof(SqlServerDistributionListAdministrationStore.UpdateDistributionListAsync));

        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(ValueTask<bool>), method.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(DistributionListAdministrationSnapshot), typeof(CancellationToken) },
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    [TestMethod]
    public void DeleteDistributionListRecipientsSql_UsesParameterizedLegacyRecipientTableAndListId()
    {
        var sql = SqlServerDistributionListAdministrationStore.DeleteDistributionListRecipientsSql;

        StringAssert.Contains(sql, "DELETE FROM hm_distributionlistsrecipients");
        StringAssert.Contains(sql, "distributionlistrecipientlistid = @LISTID");
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("@LISTID'", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteDistributionListSql_UsesParameterizedOwnerScopedLegacyListTableAndIds()
    {
        var sql = SqlServerDistributionListAdministrationStore.DeleteDistributionListSql;

        StringAssert.Contains(sql, "DELETE FROM hm_distributionlists");
        StringAssert.Contains(sql, "distributionlistdomainid = @DomainID");
        StringAssert.Contains(sql, "distributionlistid = @LISTID");
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("@LISTID'", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteDistributionListAsync_ExposesOwnerIdListIdAndCancellationContract()
    {
        var method = typeof(SqlServerDistributionListAdministrationStore).GetMethod(
            nameof(SqlServerDistributionListAdministrationStore.DeleteDistributionListAsync));

        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(ValueTask<bool>), method.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(int), typeof(int), typeof(CancellationToken) },
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }
}
