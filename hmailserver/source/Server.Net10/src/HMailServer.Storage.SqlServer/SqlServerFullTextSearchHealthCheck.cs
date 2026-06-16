using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerFullTextSearchHealthCheck
{
    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerFullTextSearchHealthCheck(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<bool> IsFullTextInstalledAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            "SELECT CONVERT(int, FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))",
            connection);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) == 1;
    }
}
