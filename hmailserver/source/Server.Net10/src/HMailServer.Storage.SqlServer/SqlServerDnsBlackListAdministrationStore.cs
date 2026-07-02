using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDnsBlackListAdministrationStore : IDnsBlackListAdministrationStore
{
    public const string GetDnsBlackListsSql = """
SELECT
    sblid,
    sblactive,
    sbldnshost,
    sblrejectmessage,
    sblresult,
    sblscore
FROM hm_dnsbl
ORDER BY sblid ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDnsBlackListAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<DnsBlackListAdministrationSnapshot>> GetDnsBlackListsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetDnsBlackListsSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var blackLists = new List<DnsBlackListAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            blackLists.Add(
                new DnsBlackListAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    Active: reader.GetInt32(1) != 0,
                    DnsHost: reader.GetString(2),
                    RejectMessage: reader.GetString(3),
                    ExpectedResult: reader.GetString(4),
                    Score: reader.GetInt32(5)));
        }

        return blackLists;
    }
}
