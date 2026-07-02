using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSurblServerAdministrationStore : ISurblServerAdministrationStore
{
    public const string GetSurblServersSql = """
SELECT
    surblid,
    surblactive,
    surblhost,
    surblrejectmessage,
    surblscore
FROM hm_surblservers
ORDER BY surblid ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerSurblServerAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<SurblServerAdministrationSnapshot>> GetSurblServersAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetSurblServersSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var servers = new List<SurblServerAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            servers.Add(
                new SurblServerAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    Active: reader.GetByte(1) != 0,
                    DnsHost: reader.GetString(2),
                    RejectMessage: reader.GetString(3),
                    Score: reader.GetInt32(4)));
        }

        return servers;
    }
}
