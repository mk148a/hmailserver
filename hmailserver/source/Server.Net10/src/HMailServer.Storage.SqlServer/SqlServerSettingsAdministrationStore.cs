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
    COALESCE(MAX(CASE WHEN settingname = N'welcomeimap' THEN settingstring END), N'')
FROM hm_settings
WHERE settingname IN (N'hostname', N'welcomesmtp', N'welcomepop3', N'welcomeimap');
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
            WelcomeImap: reader.GetString(3));
    }
}
