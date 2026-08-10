using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSettingsAdministrationStoreTests
{
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
}
