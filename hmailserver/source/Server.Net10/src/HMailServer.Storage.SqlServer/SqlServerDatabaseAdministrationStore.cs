using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDatabaseAdministrationStore : IDatabaseAdministrationStore
{
    public const int RequiredDatabaseVersion = 5708;

    public const string CurrentVersionSql = """
SELECT TOP (1) [value]
FROM hm_dbversion;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly LegacyDatabaseConfiguration _configuration;

    public SqlServerDatabaseAdministrationStore(
        SqlServerConnectionFactory connectionFactory,
        LegacyDatabaseConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        _connectionFactory = connectionFactory;
        _configuration = configuration;
    }

    public async ValueTask<DatabaseAdministrationSnapshot> GetDatabaseAsync(
        CancellationToken cancellationToken)
    {
        int? currentVersion = null;
        var isConnected = false;

        await using var connection = await TryOpenAsync(cancellationToken).ConfigureAwait(false);
        if (connection is not null)
        {
            isConnected = true;
            await using var command = new SqlCommand(CurrentVersionSql, connection);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            currentVersion = result is null || result is DBNull
                ? 0
                : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        return new DatabaseAdministrationSnapshot(
            RequiredVersion: RequiredDatabaseVersion,
            CurrentVersion: currentVersion,
            DatabaseType: _configuration.DatabaseType,
            DatabaseExists: _configuration.DatabaseExists,
            IsConnected: isConnected,
            ServerName: _configuration.ServerName,
            DatabaseName: _configuration.DatabaseName);
    }

    private async ValueTask<SqlConnection?> TryOpenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }
}
