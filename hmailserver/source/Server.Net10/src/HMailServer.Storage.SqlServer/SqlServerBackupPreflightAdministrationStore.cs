using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerBackupPreflightAdministrationStore : IBackupPreflightAdministrationStore
{
    public const string AreAllMessageFilesInDataDirectorySql = """
SELECT CASE WHEN COUNT_BIG(*) = 0 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
FROM hm_messages
WHERE LEFT(messagefilename, LEN(@DataDirectory)) <> @DataDirectory
  AND LEFT(messagefilename, 1) <> N'{';
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerBackupPreflightAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<bool> AreAllMessageFilesInDataDirectoryAsync(
        string dataDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataDirectory);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = new SqlCommand(
            AreAllMessageFilesInDataDirectorySql,
            connection);
        command.Parameters.Add("@DataDirectory", SqlDbType.NVarChar, 260).Value = dataDirectory;

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null
            && result is not DBNull
            && Convert.ToBoolean(result);
    }
}
