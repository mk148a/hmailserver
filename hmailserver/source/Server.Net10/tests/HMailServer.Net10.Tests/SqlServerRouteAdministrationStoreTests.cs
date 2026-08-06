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

    [TestMethod]
    public void InsertRouteSql_UsesLegacyRouteTableColumnsAndIdentityOutput()
    {
        var sql = SqlServerRouteAdministrationStore.InsertRouteSql;
        StringAssert.Contains(sql, "INSERT INTO hm_routes");
        foreach (var column in new[]
        {
            "routedomainname",
            "routedescription",
            "routetargetsmthost",
            "routetargetsmtport",
            "routenooftries",
            "routeminutesbetweentry",
            "routealladdresses",
            "routeuseauthentication",
            "routeauthenticationusername",
            "routeauthenticationpassword",
            "routetreatsecurityaslocal",
            "routetreatsenderaslocaldomain",
            "routeconnectionsecurity"
        })
        {
            StringAssert.Contains(sql, column);
        }

        StringAssert.Contains(sql, "OUTPUT INSERTED.routeid");
        StringAssert.Contains(sql, "@DomainName");
        StringAssert.Contains(sql, "@Description");
        StringAssert.Contains(sql, "@TargetSmtpHost");
        StringAssert.Contains(sql, "@TargetSmtpPort");
        StringAssert.Contains(sql, "@NumberOfTries");
        StringAssert.Contains(sql, "@MinutesBetweenTry");
        StringAssert.Contains(sql, "@AllAddresses");
        StringAssert.Contains(sql, "@RelayerRequiresAuth");
        StringAssert.Contains(sql, "@RelayerAuthUsername");
        StringAssert.Contains(sql, "@RelayerAuthPassword");
        StringAssert.Contains(sql, "@TreatRecipientAsLocalDomain");
        StringAssert.Contains(sql, "@TreatSenderAsLocalDomain");
        StringAssert.Contains(sql, "@ConnectionSecurity");
        Assert.IsFalse(sql.Contains("WHERE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateRouteSql_UsesLegacyRouteTableColumnsAndIdentityPredicate()
    {
        var sql = SqlServerRouteAdministrationStore.UpdateRouteSql;
        StringAssert.Contains(sql, "UPDATE hm_routes");
        foreach (var column in new[]
        {
            "routedomainname",
            "routedescription",
            "routetargetsmthost",
            "routetargetsmtport",
            "routenooftries",
            "routeminutesbetweentry",
            "routealladdresses",
            "routeuseauthentication",
            "routeauthenticationusername",
            "routeauthenticationpassword",
            "routetreatsecurityaslocal",
            "routetreatsenderaslocaldomain",
            "routeconnectionsecurity"
        })
        {
            StringAssert.Contains(sql, $"{column} = @");
        }

        StringAssert.Contains(sql, "WHERE routeid = @ID");
        StringAssert.Contains(sql, "@ID");
        StringAssert.Contains(sql, "@RelayerAuthPassword");
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }
}