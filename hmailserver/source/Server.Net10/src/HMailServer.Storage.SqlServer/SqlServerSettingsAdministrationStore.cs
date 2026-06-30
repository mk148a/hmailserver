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
    COALESCE(MAX(CASE WHEN settingname = N'protocolimap' THEN settinginteger END), 0)
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
    N'protocolimap'
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
            ServiceImap: reader.GetInt32(10) != 0);
    }
}
