using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSettingsAdministrationStoreTests
{
    [TestMethod]
    public void GetSettingsSql_ReadsOnlyLegacyHostWelcomeLimitProtocolAndRetryScalars()
    {
        var sql = SqlServerSettingsAdministrationStore.GetSettingsSql;

        StringAssert.Contains(sql, "settingstring");
        StringAssert.Contains(sql, "settinginteger");
        StringAssert.Contains(sql, "FROM hm_settings");
        StringAssert.Contains(sql, "settingname = N'hostname'");
        StringAssert.Contains(sql, "settingname = N'welcomesmtp'");
        StringAssert.Contains(sql, "settingname = N'welcomepop3'");
        StringAssert.Contains(sql, "settingname = N'welcomeimap'");
        StringAssert.Contains(sql, "settingname = N'maxsmtpconnections'");
        StringAssert.Contains(sql, "settingname = N'maxpop3connections'");
        StringAssert.Contains(sql, "settingname = N'maximapconnections'");
        StringAssert.Contains(sql, "settingname = N'maxdelivertythreads'");
        StringAssert.Contains(sql, "settingname = N'protocolsmtp'");
        StringAssert.Contains(sql, "settingname = N'protocolpop3'");
        StringAssert.Contains(sql, "settingname = N'protocolimap'");
        StringAssert.Contains(sql, "settingname = N'smtpnoofretries'");
        StringAssert.Contains(sql, "settingname = N'smtpminutesbetweenretries'");
        Assert.IsFalse(sql.Contains("N'smtpnooftries'", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void GetSettingsSql_RemainsReadOnlyAndExcludesSecrets()
    {
        var sql = SqlServerSettingsAdministrationStore.GetSettingsSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("smtprelayer", StringComparison.OrdinalIgnoreCase));
    }
}
