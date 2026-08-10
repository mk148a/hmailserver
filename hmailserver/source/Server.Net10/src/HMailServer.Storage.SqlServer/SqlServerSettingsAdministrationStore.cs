using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSettingsAdministrationStore :
    ISettingsAdministrationStore,
    ISettingsAdministrationMutationStore,
    IBackupSettingsPropertyStore
{
    public const string UpdateDefaultDomainSql = """
UPDATE hm_settings
SET settingstring = @DefaultDomain
WHERE settingname = N'defaultdomain';
""";

    public const string UpdateMirrorEmailAddressSql = """
UPDATE hm_settings
SET settingstring = @MirrorEmailAddress
WHERE settingname = N'mirroremailaddress';
""";

    public const string UpdateWorkerThreadPrioritySql = """
UPDATE hm_settings
SET settinginteger = @WorkerThreadPriority
WHERE settingname = N'workerthreadpriority';
""";

    public const string UpdateMaxSmtpConnectionsSql = """
UPDATE hm_settings
SET settinginteger = @MaxSMTPConnections
WHERE settingname = N'maxsmtpconnections';
""";

    public const string GetSettingsSql = """
SELECT
    COALESCE(MAX(CASE WHEN settingname = N'hostname' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'welcomesmtp' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'welcomepop3' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'welcomeimap' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'maxsmtpconnections' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'maxpop3connections' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'maximapconnections' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'maxdelivertythreads' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'protocolsmtp' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'protocolpop3' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'protocolimap' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'smtpnoofretries' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'smtpminutesbetweenretries' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'maxmessagesize' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'maxsmtprecipientsinbatch' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'disconnectinvalidclients' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'maximumincorrectcommands' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'enableimapsort' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'enableimapquota' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'enableimapidle' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'enableimapacl' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'EnableImapSASLPlain' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'EnableImapSASLInitialResponse' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'imappublicfoldername' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'IMAPHierarchyDelimiter' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'authallowplaintext' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'allowmailfromnull' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'smtpallowincorrectlineendings' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'adddeliveredtoheader' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'mirroremailaddress' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'defaultdomain' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'smtpdeliverybindtoip' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'rulelooplimit' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'workerthreadpriority' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'tcpipthreads' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'MaxNumberOfMXHosts' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'VerifyRemoteSslCertificate' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'SslCipherList' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'IPv6Preferred' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'AutoBanOnLogonFailureEnabled' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'MaxInvalidLogonAttempts' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'LogonAttemptsWithinMinutes' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'AutoBanMinutes' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'smtprelayer' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'usesmtprelayerauthentication' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'smtprelayerusername' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'smtprelayerport' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'smtprelayerconnectionsecurity' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'SmtpDeliveryConnectionSecurity' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'SslVersions' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'TlsOptions' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'ImapMasterUser' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'MaxNumberOfAsynchronousTasks' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'logging' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'logdevice' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'logformat' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'awstatsenabled' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'usescriptserver' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'scriptlanguage' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'backupdestination' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'backupoptions' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'avclamwinenable' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'avclamwinexec' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'avclamwindb' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'avaction' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'avnotifyreceiver' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'avnotifysender' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'usecustomvirusscanner' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'customvirusscannerexecutable' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'customviursscannerreturnvalue' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'avmaxmsgsize' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'enableattachmentblocking' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'ClamAVEnabled' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'ClamAVHost' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'ClamAVPort' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'usegreylisting' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'greylistinginitialdelay' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'greylistinginitialdelete' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'greylistingfinaldelete' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'ascheckhostinhelo' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'ascheckhostinheloscore' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'ascheckptr' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'ascheckptrscore' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'antispamaddheaderspam' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'antispamaddheaderreason' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'antispamprependsubject' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'antispamprependsubjecttext' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'spammarkthreshold' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'spamdeletethreshold' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'usespf' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'usespfscore' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'usemxchecks' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'usemxchecksscore' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'spamassassinenabled' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'spamassassinscore' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'spamassassinmergescore' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'spamassassinhost' THEN settingstring END), N''),
    COALESCE(MAX(CASE WHEN settingname = N'spamassassinport' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'antispammaxsize' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'ASDKIMVerificationEnabled' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'ASDKIMVerificationFailureScore' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'BypassGreylistingOnSPFSuccess' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'BypassGreylistingOnMailFromMX' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'usecache' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'domaincachettl' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'accountcachettl' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'aliascachettl' THEN settinginteger END), 0),
    COALESCE(MAX(CASE WHEN settingname = N'distributionlistcachettl' THEN settinginteger END), 0)
FROM hm_settings
WHERE settingname IN
(
    N'hostname',
    N'welcomesmtp',
    N'welcomepop3',
    N'welcomeimap',
    N'maxsmtpconnections',
    N'maxpop3connections',
    N'maximapconnections',
    N'maxdelivertythreads',
    N'protocolsmtp',
    N'protocolpop3',
    N'protocolimap',
    N'smtpnoofretries',
    N'smtpminutesbetweenretries',
    N'maxmessagesize',
    N'maxsmtprecipientsinbatch',
    N'disconnectinvalidclients',
    N'maximumincorrectcommands',
    N'enableimapsort',
    N'enableimapquota',
    N'enableimapidle',
    N'enableimapacl',
    N'EnableImapSASLPlain',
    N'EnableImapSASLInitialResponse',
    N'imappublicfoldername',
    N'IMAPHierarchyDelimiter',
    N'authallowplaintext',
    N'allowmailfromnull',
    N'smtpallowincorrectlineendings',
    N'adddeliveredtoheader',
    N'mirroremailaddress',
    N'defaultdomain',
    N'smtpdeliverybindtoip',
    N'rulelooplimit',
    N'workerthreadpriority',
    N'tcpipthreads',
    N'MaxNumberOfMXHosts',
    N'VerifyRemoteSslCertificate',
    N'SslCipherList',
    N'IPv6Preferred',
    N'AutoBanOnLogonFailureEnabled',
    N'MaxInvalidLogonAttempts',
    N'LogonAttemptsWithinMinutes',
    N'AutoBanMinutes',
    N'smtprelayer',
    N'usesmtprelayerauthentication',
    N'smtprelayerusername',
    N'smtprelayerport',
    N'smtprelayerconnectionsecurity',
    N'SmtpDeliveryConnectionSecurity',
    N'SslVersions',
    N'TlsOptions',
    N'ImapMasterUser',
    N'MaxNumberOfAsynchronousTasks',
    N'logging',
    N'logdevice',
    N'logformat',
    N'awstatsenabled',
    N'usescriptserver',
    N'scriptlanguage',
    N'backupdestination',
    N'backupoptions',
    N'avclamwinenable',
    N'avclamwinexec',
    N'avclamwindb',
    N'avaction',
    N'avnotifyreceiver',
    N'avnotifysender',
    N'usecustomvirusscanner',
    N'customvirusscannerexecutable',
    N'customviursscannerreturnvalue',
    N'avmaxmsgsize',
    N'enableattachmentblocking',
    N'ClamAVEnabled',
    N'ClamAVHost',
    N'ClamAVPort',
    N'usegreylisting',
    N'greylistinginitialdelay',
    N'greylistinginitialdelete',
    N'greylistingfinaldelete',
    N'ascheckhostinhelo',
    N'ascheckhostinheloscore',
    N'ascheckptr',
    N'ascheckptrscore',
    N'antispamaddheaderspam',
    N'antispamaddheaderreason',
    N'antispamprependsubject',
    N'antispamprependsubjecttext',
    N'spammarkthreshold',
    N'spamdeletethreshold',
    N'usespf',
    N'usespfscore',
    N'usemxchecks',
    N'usemxchecksscore',
    N'spamassassinenabled',
    N'spamassassinscore',
    N'spamassassinmergescore',
    N'spamassassinhost',
    N'spamassassinport',
    N'antispammaxsize',
    N'ASDKIMVerificationEnabled',
    N'ASDKIMVerificationFailureScore',
    N'BypassGreylistingOnSPFSuccess',
    N'BypassGreylistingOnMailFromMX',
    N'usecache',
    N'domaincachettl',
    N'accountcachettl',
    N'aliascachettl',
    N'distributionlistcachettl'
);
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public const string GetBackupSettingsPropertiesSql = """
SELECT settingname, settinginteger, settingstring
FROM hm_settings
WHERE settingname <> N'smtprelayerpassword'
""";

    public SqlServerSettingsAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<bool> UpdateDefaultDomainAsync(
        string defaultDomain,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateDefaultDomainSql, connection);
        command.Parameters.Add("@DefaultDomain", SqlDbType.NVarChar, 255).Value = defaultDomain;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<bool> UpdateMirrorEmailAddressAsync(
        string mirrorEmailAddress,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateMirrorEmailAddressSql, connection);
        command.Parameters.Add("@MirrorEmailAddress", SqlDbType.NVarChar, 255).Value = mirrorEmailAddress;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<bool> UpdateWorkerThreadPriorityAsync(
        int workerThreadPriority,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateWorkerThreadPrioritySql, connection);
        command.Parameters.Add("@WorkerThreadPriority", SqlDbType.Int).Value = workerThreadPriority;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<bool> UpdateMaxSmtpConnectionsAsync(
        int maxSmtpConnections,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateMaxSmtpConnectionsSql, connection);
        command.Parameters.Add("@MaxSMTPConnections", SqlDbType.Int).Value = maxSmtpConnections;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetSettingsSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess | CommandBehavior.SingleRow,
            cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new SettingsAdministrationSnapshot(string.Empty, string.Empty, string.Empty, string.Empty);
        }

        return new SettingsAdministrationSnapshot(
            HostName: reader.GetString(0),
            WelcomeSmtp: reader.GetString(1),
            WelcomePop3: reader.GetString(2),
            WelcomeImap: reader.GetString(3),
            MaxSmtpConnections: reader.GetInt32(4),
            MaxPop3Connections: reader.GetInt32(5),
            MaxImapConnections: reader.GetInt32(6),
            MaxDeliveryThreads: reader.GetInt32(7),
            ServiceSmtp: reader.GetInt32(8) != 0,
            ServicePop3: reader.GetInt32(9) != 0,
            ServiceImap: reader.GetInt32(10) != 0,
            SmtpNoOfTries: reader.GetInt32(11),
            SmtpMinutesBetweenTry: reader.GetInt32(12),
            MaxMessageSize: reader.GetInt32(13),
            MaxSmtpRecipientsInBatch: reader.GetInt32(14),
            DisconnectInvalidClients: reader.GetInt32(15) != 0,
            MaxNumberOfInvalidCommands: reader.GetInt32(16),
            ImapSortEnabled: reader.GetInt32(17) != 0,
            ImapQuotaEnabled: reader.GetInt32(18) != 0,
            ImapIdleEnabled: reader.GetInt32(19) != 0,
            ImapAclEnabled: reader.GetInt32(20) != 0,
            ImapSaslPlainEnabled: reader.GetInt32(21) != 0,
            ImapSaslInitialResponseEnabled: reader.GetInt32(22) != 0,
            ImapPublicFolderName: reader.GetString(23),
            ImapHierarchyDelimiter: reader.GetString(24),
            AllowSmtpAuthPlain: reader.GetInt32(25) != 0,
            AllowMailFromNull: reader.GetInt32(26) != 0,
            AllowIncorrectLineEndings: reader.GetInt32(27) != 0,
            AddDeliveredToHeader: reader.GetInt32(28) != 0,
            MirrorEmailAddress: reader.GetString(29),
            DefaultDomain: reader.GetString(30),
            SmtpDeliveryBindToIp: reader.GetString(31),
            RuleLoopLimit: reader.GetInt32(32),
            WorkerThreadPriority: reader.GetInt32(33),
            TcpIpThreads: reader.GetInt32(34),
            MaxNumberOfMxHosts: reader.GetInt32(35),
            VerifyRemoteSslCertificate: reader.GetInt32(36) != 0,
            SslCipherList: reader.GetString(37),
            Ipv6PreferredEnabled: reader.GetInt32(38) != 0,
            AutoBanOnLogonFailure: reader.GetInt32(39) != 0,
            MaxInvalidLogonAttempts: reader.GetInt32(40),
            MaxInvalidLogonAttemptsWithin: reader.GetInt32(41),
            AutoBanMinutes: reader.GetInt32(42),
            SmtpRelayer: reader.GetString(43),
            SmtpRelayerRequiresAuthentication: reader.GetInt32(44) != 0,
            SmtpRelayerUsername: reader.GetString(45),
            SmtpRelayerPort: reader.GetInt32(46),
            SmtpRelayerConnectionSecurity: reader.GetInt32(47),
            SmtpConnectionSecurity: reader.GetInt32(48),
            SslVersions: reader.GetInt32(49),
            TlsOptions: reader.GetInt32(50),
            ImapMasterUser: reader.GetString(51),
            MaxAsynchronousThreads: reader.GetInt32(52),
            LoggingMask: reader.GetInt32(53),
            LogDevice: reader.GetInt32(54),
            LogFormat: reader.GetInt32(55),
            AwStatsEnabled: reader.GetInt32(56) != 0,
            UseScriptServer: reader.GetInt32(57) != 0,
            ScriptLanguage: reader.GetString(58),
            BackupDestination: reader.GetString(59),
            BackupOptions: reader.GetInt32(60),
            AntiVirusClamWinEnabled: reader.GetInt32(61) != 0,
            AntiVirusClamWinExecutable: reader.GetString(62),
            AntiVirusClamWinDatabase: reader.GetString(63),
            AntiVirusAction: reader.GetInt32(64),
            AntiVirusNotifyReceiver: reader.GetInt32(65) != 0,
            AntiVirusNotifySender: reader.GetInt32(66) != 0,
            AntiVirusCustomScannerEnabled: reader.GetInt32(67) != 0,
            AntiVirusCustomScannerExecutable: reader.GetString(68),
            AntiVirusCustomScannerReturnValue: reader.GetInt32(69),
            AntiVirusMaximumMessageSize: reader.GetInt32(70),
            AntiVirusEnableAttachmentBlocking: reader.GetInt32(71) != 0,
            AntiVirusClamAvEnabled: reader.GetInt32(72) != 0,
            AntiVirusClamAvHost: reader.GetString(73),
            AntiVirusClamAvPort: reader.GetInt32(74),
            AntiSpamGreyListingEnabled: reader.GetInt32(75) != 0,
            AntiSpamGreyListingInitialDelay: reader.GetInt32(76),
            AntiSpamGreyListingInitialDelete: reader.GetInt32(77),
            AntiSpamGreyListingFinalDelete: reader.GetInt32(78),
            AntiSpamCheckHostInHelo: reader.GetInt32(79) != 0,
            AntiSpamCheckHostInHeloScore: reader.GetInt32(80),
            AntiSpamCheckPtr: reader.GetInt32(81) != 0,
            AntiSpamCheckPtrScore: reader.GetInt32(82),
            AntiSpamAddHeaderSpam: reader.GetInt32(83) != 0,
            AntiSpamAddHeaderReason: reader.GetInt32(84) != 0,
            AntiSpamPrependSubject: reader.GetInt32(85) != 0,
            AntiSpamPrependSubjectText: reader.GetString(86),
            AntiSpamSpamMarkThreshold: reader.GetInt32(87),
            AntiSpamSpamDeleteThreshold: reader.GetInt32(88),
            AntiSpamUseSpf: reader.GetInt32(89) != 0,
            AntiSpamUseSpfScore: reader.GetInt32(90),
            AntiSpamUseMxChecks: reader.GetInt32(91) != 0,
            AntiSpamUseMxChecksScore: reader.GetInt32(92),
            AntiSpamSpamAssassinEnabled: reader.GetInt32(93) != 0,
            AntiSpamSpamAssassinScore: reader.GetInt32(94),
            AntiSpamSpamAssassinMergeScore: reader.GetInt32(95) != 0,
            AntiSpamSpamAssassinHost: reader.GetString(96),
            AntiSpamSpamAssassinPort: reader.GetInt32(97),
            AntiSpamMaximumMessageSize: reader.GetInt32(98),
            AntiSpamDkimVerificationEnabled: reader.GetInt32(99) != 0,
            AntiSpamDkimVerificationFailureScore: reader.GetInt32(100),
            AntiSpamBypassGreylistingOnSpfSuccess: reader.GetInt32(101) != 0,
            AntiSpamBypassGreylistingOnMailFromMx: reader.GetInt32(102) != 0,
            CacheEnabled: reader.GetInt32(103) != 0,
            DomainCacheTtl: reader.GetInt32(104),
            AccountCacheTtl: reader.GetInt32(105),
            AliasCacheTtl: reader.GetInt32(106),
            DistributionListCacheTtl: reader.GetInt32(107));
    }

    public async ValueTask<IReadOnlyList<BackupSettingsPropertySnapshot>>
        GetBackupSettingsPropertiesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = new SqlCommand(GetBackupSettingsPropertiesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var properties = new List<BackupSettingsPropertySnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            properties.Add(new BackupSettingsPropertySnapshot(
                Name: reader.GetString(0),
                LongValue: reader.GetInt32(1),
                StringValue: reader.GetString(2)));
        }

        properties.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.Name, right.Name));
        return properties;
    }
}
