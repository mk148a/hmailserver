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
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectVerifyRemoteSslCertificateSql, "settinginteger");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectVerifyRemoteSslCertificateSql, "settingname = N'VerifyRemoteSslCertificate'");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectMaxNumberOfMxHostsSql, "settinginteger");
        StringAssert.Contains(SqlServerDeliveryTargetResolver.SelectMaxNumberOfMxHostsSql, "settingname = N'MaxNumberOfMXHosts'");
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

    [TestMethod]
    public void TargetResolverSource_PropagatesMaxNumberOfMxHostsToMatchedAndForcedRoutes()
    {
        var source = ReadResolverSource();
        var forcedRouteStart = source.IndexOf("else if (forcedRoute is not null)", StringComparison.Ordinal);
        var forcedRouteEnd = source.IndexOf("else\n            {", forcedRouteStart, StringComparison.Ordinal);
        var matchedRouteStart = source.IndexOf("if (route is not null)", StringComparison.Ordinal);
        var matchedRouteEnd = source.IndexOf("var smtpRelayer = await loadSmtpRelayerAsync", matchedRouteStart, StringComparison.Ordinal);

        Assert.IsTrue(forcedRouteStart >= 0);
        Assert.IsTrue(forcedRouteEnd > forcedRouteStart);
        Assert.IsTrue(matchedRouteStart >= 0);
        Assert.IsTrue(matchedRouteEnd > matchedRouteStart);

        var forcedRouteBranch = source.Substring(forcedRouteStart, forcedRouteEnd - forcedRouteStart);
        var matchedRouteBranch = source.Substring(matchedRouteStart, matchedRouteEnd - matchedRouteStart);

        StringAssert.Contains(forcedRouteBranch, "maxNumberOfMxHosts ??= await LoadMaxNumberOfMxHostsAsync");
        StringAssert.Contains(forcedRouteBranch, "maxNumberOfMxHosts.Value");
        StringAssert.Contains(source, "MaxNumberOfMxHosts: maxNumberOfMxHosts);");
        StringAssert.Contains(matchedRouteBranch, "MaxNumberOfMxHosts: await loadMaxNumberOfMxHostsAsync().ConfigureAwait(false)");
    }

    [TestMethod]
    public void TargetResolverSource_DefaultsMissingOrNegativeMaxNumberOfMxHostsAndPropagatesConversionErrors()
    {
        var source = ReadResolverSource();
        var loadStart = source.IndexOf("private static async ValueTask<int> LoadMaxNumberOfMxHostsAsync", StringComparison.Ordinal);
        var loadEnd = source.IndexOf("private static async ValueTask<RelayerInfo?> LoadSmtpRelayerAsync", loadStart, StringComparison.Ordinal);

        Assert.IsTrue(loadStart >= 0);
        Assert.IsTrue(loadEnd > loadStart);
        var loadMethod = source.Substring(loadStart, loadEnd - loadStart);

        StringAssert.Contains(loadMethod, "return value is null or DBNull");
        StringAssert.Contains(loadMethod, "? 0");
        StringAssert.Contains(loadMethod, "Math.Max(0, Convert.ToInt32(value");
        Assert.IsFalse(loadMethod.Contains("catch", StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadResolverSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine(
                directory.FullName,
                "hmailserver",
                "source",
                "Server.Net10",
                "src",
                "HMailServer.Storage.SqlServer",
                "SqlServerDeliveryTargetResolver.cs");

            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        Assert.Fail("Could not locate SqlServerDeliveryTargetResolver.cs from the test output directory.");
        return string.Empty;
    }
}
