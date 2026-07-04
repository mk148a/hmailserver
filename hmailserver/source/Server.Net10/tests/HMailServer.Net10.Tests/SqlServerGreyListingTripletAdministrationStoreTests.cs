using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerGreyListingTripletAdministrationStoreTests
{
    [TestMethod]
    public void ClearAllSql_PreservesLegacyAllRowDelete()
    {
        var sql = SqlServerGreyListingTripletAdministrationStore.ClearAllSql;

        Assert.AreEqual("DELETE FROM hm_greylisting_triplets;", sql);
        Assert.IsFalse(sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_greylisting_whiteaddresses", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("EXEC", StringComparison.OrdinalIgnoreCase));
    }
}
