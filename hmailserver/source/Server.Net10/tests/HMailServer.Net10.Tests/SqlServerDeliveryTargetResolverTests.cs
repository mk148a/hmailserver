using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDeliveryTargetResolverTests
{
    [TestMethod]
    public void TargetResolverSql_LoadsGlobalSmtpConnectionSecurityAsAConstantSettingLookup()
    {
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectSmtpConnectionSecuritySql, "FROM hm_settings");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectSmtpConnectionSecuritySql, "settinginteger");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectSmtpConnectionSecuritySql, "settingname = N'SmtpDeliveryConnectionSecurity'");
    }

    [TestMethod]
    public void TargetResolverSql_LoadsLegacySmtpRelayerSettingsOnlyForDelivery()
    {
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectSmtpRelayerSql, "smtprelayer");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectSmtpRelayerSql, "smtprelayerpassword");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectSmtpRelayerSql, "smtprelayerport");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectSmtpRelayerSql, "smtprelayerconnectionsecurity");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectSmtpRelayerSql, "usesmtprelayerauthentication");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectSmtpRelayerSql, "WHERE settingname IN");
    }

    [TestMethod]
    public void TargetResolverSql_LoadsLegacyGlobalSmtpRelayerSettingsAndCredential()
    {
        var sql = SqlServerDeliveryTargetResolver.SelectSmtpRelayerSql;

        StringAssert.Contains(sql, "FROM hm_settings");
        StringAssert.Contains(sql, "settingname = N'smtprelayer'");
        StringAssert.Contains(sql, "settingname = N'usesmtprelayerauthentication'");
        StringAssert.Contains(sql, "settingname = N'smtprelayerusername'");
        StringAssert.Contains(sql, "settingname = N'smtprelayerport'");
        StringAssert.Contains(sql, "settingname = N'smtprelayerconnectionsecurity'");
        StringAssert.Contains(sql, "settingname = N'smtprelayerpassword'");
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
    }

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
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRouteByIdSql, "FROM hm_routes");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectRouteByIdSql, "WHERE routeid = @RouteId");
    }
}
