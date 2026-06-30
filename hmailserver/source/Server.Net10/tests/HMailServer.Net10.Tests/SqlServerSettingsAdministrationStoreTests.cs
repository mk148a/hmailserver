using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSettingsAdministrationStoreTests
{
    [TestMethod]
    public void GetSettingsSql_ReadsOnlyLegacyHostAndWelcomeStrings()
    {
        var sql = SqlServerSettingsAdministrationStore.GetSettingsSql;

        StringAssert.Contains(sql, "settingstring");
        StringAssert.Contains(sql, "FROM hm_settings");
        StringAssert.Contains(sql, "settingname = N'hostname'");
        StringAssert.Contains(sql, "settingname = N'welcomesmtp'");
        StringAssert.Contains(sql, "settingname = N'welcomepop3'");
        StringAssert.Contains(sql, "settingname = N'welcomeimap'");
    }

    [TestMethod]
    public void GetSettingsSql_RemainsReadOnlyAndExcludesSecretOrRuntimeConfiguration()
    {
        var sql = SqlServerSettingsAdministrationStore.GetSettingsSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("smtprelayer", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("protocolsmtp", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("protocolpop3", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("protocolimap", StringComparison.OrdinalIgnoreCase));
    }
}
