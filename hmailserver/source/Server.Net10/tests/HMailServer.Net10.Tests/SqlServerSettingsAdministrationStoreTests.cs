using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSettingsAdministrationStoreTests
{
    private static void AssertSql(string expected, string actual)
    {
        Assert.AreEqual(expected, actual.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public void UpdateDefaultDomainSql_UpdatesOnlyTheExistingDefaultDomainRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateDefaultDomainSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settingstring = @DefaultDomain");
        StringAssert.Contains(sql, "WHERE settingname = N'defaultdomain'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMirrorEmailAddressSql_UpdatesOnlyTheExistingMirrorRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMirrorEmailAddressSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settingstring = @MirrorEmailAddress");
        StringAssert.Contains(sql, "WHERE settingname = N'mirroremailaddress'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateAllowSmtpAuthPlainSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateAllowSmtpAuthPlainSql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @AllowSMTPAuthPlain\nWHERE settingname = N'authallowplaintext';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSmtpRelayerRequiresAuthenticationSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSmtpRelayerRequiresAuthenticationSql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @SMTPRelayerRequiresAuthentication\nWHERE settingname = N'usesmtprelayerauthentication';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSmtpRelayerSql_UsesTheExactParameterizedNVarChar4000FixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSmtpRelayerSql;

        AssertSql(
            "UPDATE hm_settings\nSET settingstring = @SMTPRelayer\nWHERE settingname = N'smtprelayer';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSmtpRelayerUsernameSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSmtpRelayerUsernameSql;

        AssertSql(
            "UPDATE hm_settings\nSET settingstring = @SMTPRelayerUsername\nWHERE settingname = N'smtprelayerusername';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSmtpRelayerPasswordSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSmtpRelayerPasswordSql;

        AssertSql(
            "UPDATE hm_settings\nSET settingstring = @SMTPRelayerPassword\nWHERE settingname = N'smtprelayerpassword';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSmtpRelayerPortSql_UsesTheExactParameterizedIntFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSmtpRelayerPortSql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @SMTPRelayerPort\nWHERE settingname = N'smtprelayerport';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSmtpRelayerConnectionSecuritySql_UsesTheExactParameterizedIntFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSmtpRelayerConnectionSecuritySql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @SMTPRelayerConnectionSecurity\nWHERE settingname = N'smtprelayerconnectionsecurity';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSmtpConnectionSecuritySql_UsesTheExactParameterizedIntFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSmtpConnectionSecuritySql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @SMTPConnectionSecurity\nWHERE settingname = N'SmtpDeliveryConnectionSecurity';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateWelcomePop3Sql_UpdatesOnlyTheExistingWelcomePop3RowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateWelcomePop3Sql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settingstring = @WelcomePOP3");
        StringAssert.Contains(sql, "WHERE settingname = N'welcomepop3'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateWelcomeSmtpSql_UpdatesOnlyTheExistingWelcomeSmtpRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateWelcomeSmtpSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settingstring = @WelcomeSMTP");
        StringAssert.Contains(sql, "WHERE settingname = N'welcomesmtp'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateWelcomeImapSql_UpdatesOnlyTheExistingWelcomeImapRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateWelcomeImapSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settingstring = @WelcomeIMAP");
        StringAssert.Contains(sql, "WHERE settingname = N'welcomeimap'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateServiceSmtpSql_UpdatesOnlyTheExistingProtocolSmtpRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateServiceSmtpSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @ServiceSMTP");
        StringAssert.Contains(sql, "WHERE settingname = N'protocolsmtp'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateServicePop3Sql_UpdatesOnlyTheExistingProtocolPop3RowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateServicePop3Sql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @ServicePOP3");
        StringAssert.Contains(sql, "WHERE settingname = N'protocolpop3'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateServiceImapSql_UpdatesOnlyTheExistingProtocolImapRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateServiceImapSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @ServiceIMAP");
        StringAssert.Contains(sql, "WHERE settingname = N'protocolimap'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSmtpDeliveryBindToIpSql_UpdatesOnlyTheExistingSettingWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSmtpDeliveryBindToIpSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settingstring = @SMTPDeliveryBindToIP");
        StringAssert.Contains(sql, "WHERE settingname = N'smtpdeliverybindtoip'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateImapSortEnabledSql_UpdatesOnlyTheExistingSettingWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateImapSortEnabledSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @IMAPSortEnabled");
        StringAssert.Contains(sql, "WHERE settingname = N'enableimapsort'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateImapQuotaEnabledSql_UpdatesOnlyTheExistingSettingWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateImapQuotaEnabledSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @IMAPQuotaEnabled");
        StringAssert.Contains(sql, "WHERE settingname = N'enableimapquota'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateImapIdleEnabledSql_UpdatesOnlyTheExistingSettingWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateImapIdleEnabledSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @IMAPIdleEnabled");
        StringAssert.Contains(sql, "WHERE settingname = N'enableimapidle'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateImapAclEnabledSql_UpdatesOnlyTheExistingSettingWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateImapAclEnabledSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @IMAPACLEnabled");
        StringAssert.Contains(sql, "WHERE settingname = N'enableimapacl'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateImapSaslPlainEnabledSql_UpdatesOnlyTheExistingSettingWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateImapSaslPlainEnabledSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @IMAPSASLPlainEnabled");
        StringAssert.Contains(sql, "WHERE settingname = N'EnableImapSASLPlain'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateImapSaslInitialResponseEnabledSql_UpdatesOnlyTheExistingSettingWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateImapSaslInitialResponseEnabledSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @IMAPSASLInitialResponseEnabled");
        StringAssert.Contains(sql, "WHERE settingname = N'EnableImapSASLInitialResponse'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateImapPublicFolderNameSql_UpdatesOnlyTheExistingSettingWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateImapPublicFolderNameSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settingstring = @IMAPPublicFolderName");
        StringAssert.Contains(sql, "WHERE settingname = N'imappublicfoldername'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateImapMasterUserSql_UpdatesOnlyTheExistingSettingWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateImapMasterUserSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settingstring = @IMAPMasterUser");
        StringAssert.Contains(sql, "WHERE settingname = N'ImapMasterUser'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateHostNameSql_UpdatesOnlyTheExistingSettingWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateHostNameSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settingstring = @HostName");
        StringAssert.Contains(sql, "WHERE settingname = N'hostname'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ImapHierarchyDelimiterSql_UsesExistingRowsAndParameterizedCrossTableChecks()
    {
        StringAssert.Contains(
            SqlServerSettingsAdministrationStore.GetImapHierarchyDelimiterForUpdateSql,
            "UPDLOCK");
        StringAssert.Contains(
            SqlServerSettingsAdministrationStore.HasImapFolderContainingDelimiterSql,
            "CHARINDEX(@NewDelimiter, foldername)");
        StringAssert.Contains(
            SqlServerSettingsAdministrationStore.HasRuleActionContainingDelimiterSql,
            "CHARINDEX(@NewDelimiter, actionimapfolder)");
        StringAssert.Contains(
            SqlServerSettingsAdministrationStore.ReplaceRuleActionHierarchyDelimiterSql,
            "REPLACE(actionimapfolder, @OldDelimiter, @NewDelimiter)");
        StringAssert.Contains(
            SqlServerSettingsAdministrationStore.UpdateImapHierarchyDelimiterSql,
            "WHERE settingname = N'IMAPHierarchyDelimiter'");
        Assert.IsFalse(
            SqlServerSettingsAdministrationStore.ReplaceRuleActionHierarchyDelimiterSql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateWorkerThreadPrioritySql_UpdatesOnlyTheExistingWorkerPriorityRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateWorkerThreadPrioritySql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @WorkerThreadPriority");
        StringAssert.Contains(sql, "WHERE settingname = N'workerthreadpriority'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateTcpIpThreadsSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateTcpIpThreadsSql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @TCPIPThreads\nWHERE settingname = N'tcpipthreads';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSmtpNoOfTriesSql_UpdatesOnlyTheExistingRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSmtpNoOfTriesSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @SMTPNoOfTries");
        StringAssert.Contains(sql, "WHERE settingname = N'smtpnoofretries'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSmtpMinutesBetweenTrySql_UpdatesOnlyTheExistingRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSmtpMinutesBetweenTrySql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @SMTPMinutesBetweenTry");
        StringAssert.Contains(sql, "WHERE settingname = N'smtpminutesbetweenretries'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMaxSmtpConnectionsSql_UpdatesOnlyTheExistingMaxSmtpConnectionsRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxSmtpConnectionsSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @MaxSMTPConnections");
        StringAssert.Contains(sql, "WHERE settingname = N'maxsmtpconnections'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMaxPop3ConnectionsSql_UpdatesOnlyTheExistingMaxPop3ConnectionsRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxPop3ConnectionsSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @MaxPOP3Connections");
        StringAssert.Contains(sql, "WHERE settingname = N'maxpop3connections'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMaxImapConnectionsSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxImapConnectionsSql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @MaxIMAPConnections\nWHERE settingname = N'maximapconnections';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMaxMessageSizeSql_UpdatesOnlyTheExistingMaxMessageSizeRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxMessageSizeSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @MaxMessageSize");
        StringAssert.Contains(sql, "WHERE settingname = N'maxmessagesize'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMaxDeliveryThreadsSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxDeliveryThreadsSql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @MaxDeliveryThreads\nWHERE settingname = N'maxdelivertythreads';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMaxAsynchronousThreadsSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxAsynchronousThreadsSql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @MaxAsynchronousThreads\nWHERE settingname = N'MaxNumberOfAsynchronousTasks';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMaxSmtpRecipientsInBatchSql_UpdatesOnlyTheExistingRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxSmtpRecipientsInBatchSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @MaxSMTPRecipientsInBatch");
        StringAssert.Contains(sql, "WHERE settingname = N'maxsmtprecipientsinbatch'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMaxNumberOfInvalidCommandsSql_UpdatesOnlyTheExistingRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxNumberOfInvalidCommandsSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @MaxNumberOfInvalidCommands");
        StringAssert.Contains(sql, "WHERE settingname = N'maximumincorrectcommands'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMaxNumberOfMXHostsSql_UpdatesOnlyTheExactExistingRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxNumberOfMXHostsSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @MaxNumberOfMXHosts");
        StringAssert.Contains(sql, "WHERE settingname = N'MaxNumberOfMXHosts'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateVerifyRemoteSslCertificateSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateVerifyRemoteSslCertificateSql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @VerifyRemoteSslCertificate\nWHERE settingname = N'VerifyRemoteSslCertificate';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateAntiSpamUseSpfSql_UsesTheLegacyFixedRows()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @UseSPF\nWHERE settingname = N'usespf';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamUseSpfSql);
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @UseSPFScore\nWHERE settingname = N'usespfscore';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamUseSpfScoreSql);
    }

    [TestMethod]
    public void UpdateAntiSpamUseMxChecksSql_UsesTheLegacyFixedRows()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @UseMXChecks\nWHERE settingname = N'usemxchecks';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamUseMxChecksSql);
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @UseMXChecksScore\nWHERE settingname = N'usemxchecksscore';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamUseMxChecksScoreSql);
    }

    [TestMethod]
    public void UpdateAntiSpamSpamAssassinSql_UsesTheLegacyFixedRows()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @SpamAssassinEnabled\nWHERE settingname = N'spamassassinenabled';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamSpamAssassinEnabledSql);
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @SpamAssassinScore\nWHERE settingname = N'spamassassinscore';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamSpamAssassinScoreSql);
    }

    [TestMethod]
    public void UpdateAntiSpamSpamAssassinMergeScoreSql_UsesTheLegacyFixedRow()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @SpamAssassinMergeScore\nWHERE settingname = N'spamassassinmergescore';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamSpamAssassinMergeScoreSql);
    }

    [TestMethod]
    public void UpdateAntiSpamSpamAssassinHostAndPortSql_UseTheLegacyFixedRows()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settingstring = @SpamAssassinHost\nWHERE settingname = N'spamassassinhost';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamSpamAssassinHostSql);
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @SpamAssassinPort\nWHERE settingname = N'spamassassinport';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamSpamAssassinPortSql);
    }

    [TestMethod]
    public void UpdateAntiSpamMaximumMessageSizeSql_UsesTheLegacyFixedRow()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @MaximumMessageSize\nWHERE settingname = N'antispammaxsize';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamMaximumMessageSizeSql);
    }

    [TestMethod]
    public void UpdateAntiSpamDkimVerificationSql_UsesTheLegacyFixedRows()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @DkimVerificationEnabled\nWHERE settingname = N'ASDKIMVerificationEnabled';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamDkimVerificationEnabledSql);
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @DkimVerificationFailureScore\nWHERE settingname = N'ASDKIMVerificationFailureScore';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamDkimVerificationFailureScoreSql);
    }

    [TestMethod]
    public void UpdateAntiSpamBypassGreylistingSql_UsesTheLegacyFixedRows()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @BypassGreylistingOnSpfSuccess\nWHERE settingname = N'BypassGreylistingOnSPFSuccess';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamBypassGreylistingOnSpfSuccessSql);
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @BypassGreylistingOnMailFromMx\nWHERE settingname = N'BypassGreylistingOnMailFromMX';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamBypassGreylistingOnMailFromMxSql);
    }

    [TestMethod]
    public void UpdateAntiSpamCheckHostInHeloSql_UsesTheLegacyFixedRows()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @CheckHostInHelo\nWHERE settingname = N'ascheckhostinhelo';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamCheckHostInHeloSql);
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @CheckHostInHeloScore\nWHERE settingname = N'ascheckhostinheloscore';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamCheckHostInHeloScoreSql);
    }

    [TestMethod]
    public void UpdateAntiSpamCheckPtrSql_UsesTheLegacyFixedRows()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @CheckPTR\nWHERE settingname = N'ascheckptr';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamCheckPtrSql);
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @CheckPTRScore\nWHERE settingname = N'ascheckptrscore';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamCheckPtrScoreSql);
    }

    [TestMethod]
    public void UpdateAntiSpamGreyListingEnabledSql_UsesTheLegacyFixedRow()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @GreyListingEnabled\nWHERE settingname = N'usegreylisting';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamGreyListingEnabledSql);
    }

    [TestMethod]
    public void UpdateAntiSpamGreyListingInitialDelaySql_UsesTheLegacyFixedRow()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @GreyListingInitialDelay\nWHERE settingname = N'greylistinginitialdelay';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamGreyListingInitialDelaySql);
    }

    [TestMethod]
    public void UpdateAntiSpamGreyListingInitialDeleteSql_UsesTheLegacyFixedRow()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @GreyListingInitialDelete\nWHERE settingname = N'greylistinginitialdelete';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamGreyListingInitialDeleteSql);
    }

    [TestMethod]
    public void UpdateAntiSpamGreyListingFinalDeleteSql_UsesTheLegacyFixedRow()
    {
        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @GreyListingFinalDelete\nWHERE settingname = N'greylistingfinaldelete';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamGreyListingFinalDeleteSql);
    }

    [TestMethod]
    public void UpdateAntiSpamAddHeaderSql_UsesTheLegacyFixedRows()
    {
        StringAssert.Contains(
            "UPDATE hm_settings\nSET settinginteger = @AddHeaderSpam\nWHERE settingname = N'antispamaddheaderspam';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamAddHeaderSpamSql);
        StringAssert.Contains(
            "UPDATE hm_settings\nSET settinginteger = @AddHeaderReason\nWHERE settingname = N'antispamaddheaderreason';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamAddHeaderReasonSql);
    }

    [TestMethod]
    public void UpdateAntiSpamPrependSubjectSql_UsesTheLegacyFixedRows()
    {
        StringAssert.Contains(
            "UPDATE hm_settings\nSET settinginteger = @PrependSubject\nWHERE settingname = N'antispamprependsubject';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamPrependSubjectSql);
        StringAssert.Contains(
            "UPDATE hm_settings\nSET settingstring = @PrependSubjectText\nWHERE settingname = N'antispamprependsubjecttext';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamPrependSubjectTextSql);
    }

    [TestMethod]
    public void UpdateAntiSpamThresholdSql_UsesTheLegacyFixedRows()
    {
        StringAssert.Contains(
            "UPDATE hm_settings\nSET settinginteger = @SpamMarkThreshold\nWHERE settingname = N'spammarkthreshold';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamSpamMarkThresholdSql);
        StringAssert.Contains(
            "UPDATE hm_settings\nSET settinginteger = @SpamDeleteThreshold\nWHERE settingname = N'spamdeletethreshold';",
            SqlServerSettingsAdministrationStore.UpdateAntiSpamSpamDeleteThresholdSql);
    }

    [TestMethod]
    public void UpdateAllowMailFromNullSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateAllowMailFromNullSql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @AllowMailFromNull\nWHERE settingname = N'allowmailfromnull';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateRuleLoopLimitSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateRuleLoopLimitSql;

        AssertSql(
            "UPDATE hm_settings\nSET settinginteger = @RuleLoopLimit\nWHERE settingname = N'rulelooplimit';",
            sql);
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateDisconnectInvalidClientsSql_UpdatesOnlyTheExistingRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateDisconnectInvalidClientsSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @DisconnectInvalidClients");
        StringAssert.Contains(sql, "WHERE settingname = N'disconnectinvalidclients'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateAddDeliveredToHeaderSql_UpdatesOnlyTheExistingRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateAddDeliveredToHeaderSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @AddDeliveredToHeader");
        StringAssert.Contains(sql, "WHERE settingname = N'adddeliveredtoheader'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateAllowIncorrectLineEndingsSql_UpdatesOnlyTheExistingRowWithAParameter()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateAllowIncorrectLineEndingsSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @AllowIncorrectLineEndings");
        StringAssert.Contains(sql, "WHERE settingname = N'smtpallowincorrectlineendings'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

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
        StringAssert.Contains(sql, "settingname = N'mirroremailaddress'");
        StringAssert.Contains(sql, "settingname = N'defaultdomain'");
        StringAssert.Contains(sql, "settingname = N'smtpdeliverybindtoip'");
        StringAssert.Contains(sql, "settingname = N'rulelooplimit'");
        StringAssert.Contains(sql, "settingname = N'workerthreadpriority'");
        StringAssert.Contains(sql, "settingname = N'tcpipthreads'");
        StringAssert.Contains(sql, "settingname = N'MaxNumberOfMXHosts'");
        Assert.IsFalse(sql.Contains("MaxAsynchronousThreads", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(sql, "settingname = N'MaxNumberOfAsynchronousTasks'");
        StringAssert.Contains(sql, "settingname = N'VerifyRemoteSslCertificate'");
        StringAssert.Contains(sql, "settingname = N'SslCipherList'");
        StringAssert.Contains(sql, "settingname = N'IPv6Preferred'");
        StringAssert.Contains(sql, "settingname = N'AutoBanOnLogonFailureEnabled'");
        StringAssert.Contains(sql, "settingname = N'MaxInvalidLogonAttempts'");
        StringAssert.Contains(sql, "settingname = N'LogonAttemptsWithinMinutes'");
        StringAssert.Contains(sql, "settingname = N'AutoBanMinutes'");
        StringAssert.Contains(sql, "settingname = N'smtprelayer'");
        StringAssert.Contains(sql, "settingname = N'usesmtprelayerauthentication'");
        StringAssert.Contains(sql, "settingname = N'smtprelayerusername'");
        StringAssert.Contains(sql, "settingname = N'smtprelayerport'");
        StringAssert.Contains(sql, "settingname = N'smtprelayerconnectionsecurity'");
        StringAssert.Contains(sql, "settingname = N'SmtpDeliveryConnectionSecurity'");
        StringAssert.Contains(sql, "settingname = N'SslVersions'");
        StringAssert.Contains(sql, "settingname = N'TlsOptions'");
        StringAssert.Contains(sql, "settingname = N'ImapMasterUser'");
        StringAssert.Contains(sql, "settingname = N'logging'");
        StringAssert.Contains(sql, "settingname = N'logdevice'");
        StringAssert.Contains(sql, "settingname = N'logformat'");
        StringAssert.Contains(sql, "settingname = N'awstatsenabled'");
        StringAssert.Contains(sql, "settingname = N'usescriptserver'");
        StringAssert.Contains(sql, "settingname = N'scriptlanguage'");
        StringAssert.Contains(sql, "settingname = N'backupdestination'");
        StringAssert.Contains(sql, "settingname = N'backupoptions'");
        StringAssert.Contains(sql, "settingname = N'avclamwinenable'");
        StringAssert.Contains(sql, "settingname = N'avclamwinexec'");
        StringAssert.Contains(sql, "settingname = N'avclamwindb'");
        StringAssert.Contains(sql, "settingname = N'avaction'");
        StringAssert.Contains(sql, "settingname = N'avnotifyreceiver'");
        StringAssert.Contains(sql, "settingname = N'avnotifysender'");
        StringAssert.Contains(sql, "settingname = N'usecustomvirusscanner'");
        StringAssert.Contains(sql, "settingname = N'customvirusscannerexecutable'");
        StringAssert.Contains(sql, "settingname = N'customviursscannerreturnvalue'");
        StringAssert.Contains(sql, "settingname = N'avmaxmsgsize'");
        StringAssert.Contains(sql, "settingname = N'enableattachmentblocking'");
        StringAssert.Contains(sql, "settingname = N'ClamAVEnabled'");
        StringAssert.Contains(sql, "settingname = N'ClamAVHost'");
        StringAssert.Contains(sql, "settingname = N'ClamAVPort'");
        StringAssert.Contains(sql, "settingname = N'usegreylisting'");
        StringAssert.Contains(sql, "settingname = N'greylistinginitialdelay'");
        StringAssert.Contains(sql, "settingname = N'greylistinginitialdelete'");
        StringAssert.Contains(sql, "settingname = N'greylistingfinaldelete'");
        StringAssert.Contains(sql, "settingname = N'ascheckhostinhelo'");
        StringAssert.Contains(sql, "settingname = N'ascheckhostinheloscore'");
        StringAssert.Contains(sql, "settingname = N'ascheckptr'");
        StringAssert.Contains(sql, "settingname = N'ascheckptrscore'");
        StringAssert.Contains(sql, "settingname = N'antispamaddheaderspam'");
        StringAssert.Contains(sql, "settingname = N'antispamaddheaderreason'");
        StringAssert.Contains(sql, "settingname = N'antispamprependsubject'");
        StringAssert.Contains(sql, "settingname = N'antispamprependsubjecttext'");
        StringAssert.Contains(sql, "settingname = N'spammarkthreshold'");
        StringAssert.Contains(sql, "settingname = N'spamdeletethreshold'");
        StringAssert.Contains(sql, "settingname = N'usespf'");
        StringAssert.Contains(sql, "settingname = N'usespfscore'");
        StringAssert.Contains(sql, "settingname = N'usemxchecks'");
        StringAssert.Contains(sql, "settingname = N'usemxchecksscore'");
        StringAssert.Contains(sql, "settingname = N'spamassassinenabled'");
        StringAssert.Contains(sql, "settingname = N'spamassassinscore'");
        StringAssert.Contains(sql, "settingname = N'spamassassinmergescore'");
        StringAssert.Contains(sql, "settingname = N'spamassassinhost'");
        StringAssert.Contains(sql, "settingname = N'spamassassinport'");
        StringAssert.Contains(sql, "settingname = N'antispammaxsize'");
        StringAssert.Contains(sql, "settingname = N'ASDKIMVerificationEnabled'");
        StringAssert.Contains(sql, "settingname = N'ASDKIMVerificationFailureScore'");
        StringAssert.Contains(sql, "settingname = N'BypassGreylistingOnSPFSuccess'");
        StringAssert.Contains(sql, "settingname = N'BypassGreylistingOnMailFromMX'");
        StringAssert.Contains(sql, "settingname = N'usecache'");
        StringAssert.Contains(sql, "settingname = N'domaincachettl'");
        StringAssert.Contains(sql, "settingname = N'accountcachettl'");
        StringAssert.Contains(sql, "settingname = N'aliascachettl'");
        StringAssert.Contains(sql, "settingname = N'distributionlistcachettl'");
    }

    [TestMethod]
    public void UpdateIpv6PreferredSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateIpv6PreferredSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @IPv6Preferred");
        StringAssert.Contains(sql, "WHERE settingname = N'IPv6Preferred'");
    }

    [TestMethod]
    public void UpdateSslCipherListSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSslCipherListSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settingstring = @SslCipherList");
        StringAssert.Contains(sql, "WHERE settingname = N'SslCipherList'");
    }

    [TestMethod]
    public void UpdateAutoBanOnLogonFailureSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateAutoBanOnLogonFailureSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @AutoBanOnLogonFailure");
        StringAssert.Contains(sql, "WHERE settingname = N'AutoBanOnLogonFailureEnabled'");
    }

    [TestMethod]
    public void UpdateMaxInvalidLogonAttemptsSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxInvalidLogonAttemptsSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @MaxInvalidLogonAttempts");
        StringAssert.Contains(sql, "WHERE settingname = N'MaxInvalidLogonAttempts'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateMaxInvalidLogonAttemptsWithinSql_UsesTheLegacyParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateMaxInvalidLogonAttemptsWithinSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @MaxInvalidLogonAttemptsWithin");
        StringAssert.Contains(sql, "WHERE settingname = N'LogonAttemptsWithinMinutes'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateAutoBanMinutesSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateAutoBanMinutesSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @AutoBanMinutes");
        StringAssert.Contains(sql, "WHERE settingname = N'AutoBanMinutes'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSslVersionsSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateSslVersionsSql;

        StringAssert.Contains(sql, "UPDATE hm_settings");
        StringAssert.Contains(sql, "SET settinginteger = @SslVersions");
        StringAssert.Contains(sql, "WHERE settingname = N'SslVersions'");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateTlsOptionsSql_UsesTheExactParameterizedFixedRowShape()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateTlsOptionsSql;

        Assert.AreEqual(
            "UPDATE hm_settings\nSET settinginteger = @TlsOptions\nWHERE settingname = N'TlsOptions';",
            sql.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
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
        Assert.IsFalse(sql.Contains("smtprelayerpassword", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hmailserver_backup.log", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_blocked_attachments", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_dnsbl", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_surblservers", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_greylisting_triplets", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_greylisting_whiteaddresses", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_whitelist", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("VirusScannerTester", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("TestClam", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("TestSpamAssassin", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DKIMVerify", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("process", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("xp_", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void GetBackupSettingsPropertiesSqlReadsRawRowsWithoutTheCredential()
    {
        var sql = SqlServerSettingsAdministrationStore.GetBackupSettingsPropertiesSql;

        StringAssert.Contains(sql, "SELECT settingname, settinginteger, settingstring");
        StringAssert.Contains(sql, "FROM hm_settings");
        StringAssert.Contains(sql, "settingname <> N'smtprelayerpassword'");
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateBackupDestinationSql_UsesTheLegacyFixedStringRow()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateBackupDestinationSql;

        Assert.AreEqual(
            "UPDATE hm_settings\nSET settingstring = @BackupDestination\nWHERE settingname = N'backupdestination';",
            sql.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("' +", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateBackupSettingsSql_UsesCurrentRowAndParameterizedBitValues()
    {
        var sql = SqlServerSettingsAdministrationStore.UpdateBackupSettingsSql;

        Assert.AreEqual(
            "UPDATE hm_settings\nSET settinginteger = (settinginteger & ~@BackupSettingsMask) | @BackupSettingsValue\nWHERE settingname = N'backupoptions';",
            sql.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }
}
