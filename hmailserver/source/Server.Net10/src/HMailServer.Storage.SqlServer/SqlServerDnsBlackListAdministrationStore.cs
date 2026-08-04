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

    public const string InsertDnsBlackListSql = """
INSERT INTO hm_dnsbl
    (sblactive, sbldnshost, sblrejectmessage, sblresult, sblscore)
OUTPUT INSERTED.sblid
VALUES (@active, @dnsHost, @rejectMessage, @expectedResult, @score);
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

    public async ValueTask<int> InsertDnsBlackListAsync(
        DnsBlackListAdministrationSnapshot blackList,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blackList);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertDnsBlackListSql, connection);
        command.Parameters.Add("@active", SqlDbType.Bit).Value = blackList.Active;
        command.Parameters.Add("@dnsHost", SqlDbType.NVarChar, 255).Value = blackList.DnsHost;
        command.Parameters.Add("@rejectMessage", SqlDbType.NVarChar, 255).Value = blackList.RejectMessage;
        command.Parameters.Add("@expectedResult", SqlDbType.NVarChar, 255).Value = blackList.ExpectedResult;
        command.Parameters.Add("@score", SqlDbType.Int).Value = blackList.Score;

        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, System.Globalization.CultureInfo.InvariantCulture);
    }
}
