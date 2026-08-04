using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerRouteAddressAdministrationStoreTests
{
    [TestMethod]
    public void GetRouteAddressesSql_UsesLegacyColumnsAndRouteFilterWithoutInventedOrderingOrMutation()
    {
        var sql = SqlServerRouteAddressAdministrationStore.GetRouteAddressesSql;

        StringAssert.Contains(sql, "routeaddressid");
        StringAssert.Contains(sql, "routeaddressrouteid");
        StringAssert.Contains(sql, "routeaddressaddress");
        StringAssert.Contains(sql, "FROM hm_routeaddresses");
        StringAssert.Contains(sql, "WHERE routeaddressrouteid = @RouteId");
        Assert.IsFalse(sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_routes", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteRouteAddressByIdSql_DeletesOnlyMatchingAddressWithinSelectedRoute()
    {
        var sql = SqlServerRouteAddressAdministrationStore.DeleteRouteAddressByIdSql;

        StringAssert.Contains(sql, "DELETE FROM hm_routeaddresses");
        StringAssert.Contains(sql, "WHERE routeaddressrouteid = @RouteId");
        StringAssert.Contains(sql, "AND routeaddressid = @Id");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_routes", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertRouteAddressSql_UsesParameterizedLegacyColumnsAndGeneratedId()
    {
        var sql = SqlServerRouteAddressAdministrationStore.InsertRouteAddressSql;

        StringAssert.Contains(sql, "INSERT INTO hm_routeaddresses");
        StringAssert.Contains(sql, "routeaddressrouteid");
        StringAssert.Contains(sql, "routeaddressaddress");
        StringAssert.Contains(sql, "OUTPUT INSERTED.routeaddressid");
        StringAssert.Contains(sql, "VALUES (@RouteId, @Address)");
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_routes", StringComparison.OrdinalIgnoreCase));
    }
}
