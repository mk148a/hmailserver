using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDeliveryTargetResolverTests
{
    [TestMethod]
    public void TargetResolverSql_LoadsRoutesNeededForDeliveryClassification()
    {
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRoutesSql, "FROM hm_routes");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRoutesSql, "routedomainname");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRoutesSql, "routetargetsmthost");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRoutesSql, "routetargetsmtport");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRoutesSql, "routeconnectionsecurity");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRoutesSql, "routetreatsecurityaslocal");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRoutesSql, "routeuseauthentication");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRoutesSql, "routeauthenticationusername");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRoutesSql, "routeauthenticationpassword");
    }
}
