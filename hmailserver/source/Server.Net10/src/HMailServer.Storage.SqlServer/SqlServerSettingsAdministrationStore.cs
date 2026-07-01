using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSettingsAdministrationStore : ISettingsAdministrationStore
{
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
    COALESCE(MAX(CASE WHEN settingname = N'scriptlanguage' THEN settingstring END), N'')
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
    N'scriptlanguage'
);
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerSettingsAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
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
            ScriptLanguage: reader.GetString(58));
    }
}
