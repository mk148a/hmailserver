using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerGreyListingWhiteAddressAdministrationStoreTests
{
    [TestMethod]
    public void GetWhiteAddressesSql_UsesLegacyColumnsAndStoredIpOrdering()
    {
        var sql = SqlServerGreyListingWhiteAddressAdministrationStore.GetWhiteAddressesSql;

        StringAssert.Contains(sql, "FROM hm_greylisting_whiteaddresses");
        StringAssert.Contains(sql, "whiteid");
        StringAssert.Contains(sql, "whiteipaddress");
        StringAssert.Contains(sql, "whiteipdescription");
        StringAssert.Contains(sql, "ORDER BY whiteipaddress ASC");
    }

    [TestMethod]
    public void GetWhiteAddressesSql_RemainsReadOnlyAndDoesNotTouchGreylistingPolicyRuntime()
    {
        var sql = SqlServerGreyListingWhiteAddressAdministrationStore.GetWhiteAddressesSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_greylisting_triplets", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("LIKE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("xp_", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertWhiteAddressSql_UsesLegacyColumnsAndGeneratedIdentity()
    {
        var sql = SqlServerGreyListingWhiteAddressAdministrationStore.InsertWhiteAddressSql;

        StringAssert.Contains(sql, "INSERT INTO hm_greylisting_whiteaddresses");
        StringAssert.Contains(sql, "whiteipaddress");
        StringAssert.Contains(sql, "whiteipdescription");
        StringAssert.Contains(sql, "OUTPUT INSERTED.whiteid");
        StringAssert.Contains(sql, "@ipAddress");
        StringAssert.Contains(sql, "@description");
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_greylisting_triplets", StringComparison.OrdinalIgnoreCase));
    }
}
