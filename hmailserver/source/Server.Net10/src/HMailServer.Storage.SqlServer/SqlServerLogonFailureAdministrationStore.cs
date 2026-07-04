using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerLogonFailureAdministrationStore
    : ILogonFailureAdministrationStore
{
    public const string ClearLegacyListSql = """
DELETE FROM hm_logon_failures
WHERE failuretime < DATEADD(minute, 1, GETDATE());
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerLogonFailureAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask ClearLegacyListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = new SqlCommand(ClearLegacyListSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
