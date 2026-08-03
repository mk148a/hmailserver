using System.Reflection;
using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerWhiteListAddressAdministrationStoreTests
{
    [TestMethod]
    public void GetWhiteListAddressesSql_UsesLegacyColumnsAndLowerIpOrdering()
    {
        var sql = SqlServerWhiteListAddressAdministrationStore.GetWhiteListAddressesSql;

        StringAssert.Contains(sql, "FROM hm_whitelist");
        StringAssert.Contains(sql, "whiteid");
        StringAssert.Contains(sql, "whiteloweripaddress1");
        StringAssert.Contains(sql, "whiteloweripaddress2");
        StringAssert.Contains(sql, "whiteupperipaddress1");
        StringAssert.Contains(sql, "whiteupperipaddress2");
        StringAssert.Contains(sql, "whiteemailaddress");
        StringAssert.Contains(sql, "whitedescription");
        StringAssert.Contains(sql, "ORDER BY whiteloweripaddress1 ASC, whiteloweripaddress2 ASC");
    }

    [TestMethod]
    public void FormatLegacyAddress_ConvertsIpv4AndIpv6Columns()
    {
        Assert.AreEqual(
            "192.0.2.1",
            FormatLegacyAddress(0xC0000201, null));
        Assert.AreEqual(
            "2001:db8::1",
            FormatLegacyAddress(0x20010DB800000000, 1));
    }

    [TestMethod]
    public void GetWhiteListAddressesSql_RemainsReadOnlyAndDoesNotTouchSmtpPolicyRuntime()
    {
        var sql = SqlServerWhiteListAddressAdministrationStore.GetWhiteListAddressesSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_greylisting", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("LIKE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("xp_", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertWhiteListAddressSql_UsesLegacyColumnsAndGeneratedIdentity()
    {
        var sql = SqlServerWhiteListAddressAdministrationStore.InsertWhiteListAddressSql;

        StringAssert.Contains(sql, "INSERT INTO hm_whitelist");
        StringAssert.Contains(sql, "whiteloweripaddress1");
        StringAssert.Contains(sql, "whiteloweripaddress2");
        StringAssert.Contains(sql, "whiteupperipaddress1");
        StringAssert.Contains(sql, "whiteupperipaddress2");
        StringAssert.Contains(sql, "whiteemailaddress");
        StringAssert.Contains(sql, "whitedescription");
        StringAssert.Contains(sql, "OUTPUT INSERTED.whiteid");
        StringAssert.Contains(sql, "@lowerIp1");
        StringAssert.Contains(sql, "@upperIp1");
        StringAssert.Contains(sql, "@emailAddress");
        StringAssert.Contains(sql, "@description");
    }

    [TestMethod]
    public void InsertWhiteListAddressSql_RemainsScopedToWhitelistPersistence()
    {
        var sql = SqlServerWhiteListAddressAdministrationStore.InsertWhiteListAddressSql;

        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_greylisting", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateWhiteListAddressSql_UsesLegacyColumnsAndIdentityPredicate()
    {
        var sql = SqlServerWhiteListAddressAdministrationStore.UpdateWhiteListAddressSql;

        StringAssert.Contains(sql, "UPDATE hm_whitelist");
        StringAssert.Contains(sql, "whiteloweripaddress1 = @lowerIp1");
        StringAssert.Contains(sql, "whiteloweripaddress2 = @lowerIp2");
        StringAssert.Contains(sql, "whiteupperipaddress1 = @upperIp1");
        StringAssert.Contains(sql, "whiteupperipaddress2 = @upperIp2");
        StringAssert.Contains(sql, "whiteemailaddress = @emailAddress");
        StringAssert.Contains(sql, "whitedescription = @description");
        StringAssert.Contains(sql, "WHERE whiteid = @id");
    }

    [TestMethod]
    public void UpdateWhiteListAddressSql_RemainsScopedToWhitelistPersistence()
    {
        var sql = SqlServerWhiteListAddressAdministrationStore.UpdateWhiteListAddressSql;

        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_greylisting", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatLegacyAddress(long address1, long? address2)
    {
        var method = typeof(SqlServerWhiteListAddressAdministrationStore).GetMethod(
            "FormatLegacyAddress",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return (string)method.Invoke(null, new object?[] { address1, address2 })!;
    }
}
