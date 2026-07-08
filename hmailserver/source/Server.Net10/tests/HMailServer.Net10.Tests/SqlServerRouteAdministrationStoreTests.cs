using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerRouteAdministrationStoreTests
{
    [TestMethod]
    public void GetRoutesSql_UsesLegacyRouteTableNonSecretColumnsAndDomainOrdering()
    {
        var sql = SqlServerRouteAdministrationStore.GetRoutesSql;

        StringAssert.Contains(sql, "routeid");
        StringAssert.Contains(sql, "routedomainname");
        StringAssert.Contains(sql, "routedescription");
        StringAssert.Contains(sql, "routetargetsmthost");
        StringAssert.Contains(sql, "routetargetsmtport");
        StringAssert.Contains(sql, "routenooftries");
        StringAssert.Contains(sql, "routeminutesbetweentry");
        StringAssert.Contains(sql, "routealladdresses");
        StringAssert.Contains(sql, "routeuseauthentication");
        StringAssert.Contains(sql, "routeauthenticationusername");
        StringAssert.Contains(sql, "routetreatsecurityaslocal");
        StringAssert.Contains(sql, "routeconnectionsecurity");
        StringAssert.Contains(sql, "routetreatsenderaslocaldomain");
        StringAssert.Contains(sql, "FROM hm_routes");
        StringAssert.Contains(sql, "ORDER BY routedomainname ASC");
        Assert.IsFalse(sql.Contains("routeauthenticationpassword", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_routeaddresses", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }
}
