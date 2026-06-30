using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSettingsAdministrationStoreTests
{
    [TestMethod]
    public void GetSettingsSql_ReadsOnlyBoundedLegacyAdministrationScalars()
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
        StringAssert.Contains(sql, "settingname = N'maxmessagesize'");
        StringAssert.Contains(sql, "settingname = N'maxsmtprecipientsinbatch'");
        StringAssert.Contains(sql, "settingname = N'disconnectinvalidclients'");
        StringAssert.Contains(sql, "settingname = N'maximumincorrectcommands'");
        StringAssert.Contains(sql, "settingname = N'enableimapsort'");
        StringAssert.Contains(sql, "settingname = N'enableimapquota'");
        StringAssert.Contains(sql, "settingname = N'enableimapidle'");
        StringAssert.Contains(sql, "settingname = N'enableimapacl'");
        StringAssert.Contains(sql, "settingname = N'EnableImapSASLPlain'");
        StringAssert.Contains(sql, "settingname = N'EnableImapSASLInitialResponse'");
        StringAssert.Contains(sql, "settingname = N'imappublicfoldername'");
        StringAssert.Contains(sql, "settingname = N'IMAPHierarchyDelimiter'");
        StringAssert.Contains(sql, "settingname = N'authallowplaintext'");
        StringAssert.Contains(sql, "settingname = N'allowmailfromnull'");
        StringAssert.Contains(sql, "settingname = N'smtpallowincorrectlineendings'");
        StringAssert.Contains(sql, "settingname = N'adddeliveredtoheader'");
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
