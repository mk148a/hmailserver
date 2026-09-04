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
    sblresult,
    sblrejectmessage,
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

    public const string UpdateDnsBlackListSql = """
UPDATE hm_dnsbl
SET
    sblactive = @active,
    sbldnshost = @dnsHost,
    sblresult = @expectedResult,
    sblrejectmessage = @rejectMessage,
    sblscore = @score
WHERE sblid = @id;
""";

    public const string DeleteDnsBlackListSql = """
DELETE FROM hm_dnsbl
WHERE sblid = @id;
""";

    public const string DeleteAllDnsBlackListsSql = """
DELETE FROM hm_dnsbl;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerDnsBlackListAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerDnsBlackListAdministrationStore(
        SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _connectionFactory = null!;
        _transactionContext = transactionContext;
    }

    public async ValueTask<IReadOnlyList<DnsBlackListAdministrationSnapshot>> GetDnsBlackListsAsync(
        CancellationToken cancellationToken)
    {
        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            GetDnsBlackListsSql,
            cancellationToken).ConfigureAwait(false);
        var command = commandLease.Command;
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
                    RejectMessage: reader.GetString(4),
                    ExpectedResult: reader.GetString(3),
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

    public async ValueTask<int> InsertDnsBlackListForRestoreAsync(
        DnsBlackListAdministrationSnapshot blackList,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blackList);

        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            InsertDnsBlackListSql,
            cancellationToken).ConfigureAwait(false);
        var command = commandLease.Command;
        command.Parameters.Add("@active", SqlDbType.Bit).Value = blackList.Active;
        command.Parameters.Add("@dnsHost", SqlDbType.NVarChar, 255).Value = blackList.DnsHost;
        command.Parameters.Add("@rejectMessage", SqlDbType.NVarChar, 255).Value = blackList.RejectMessage;
        command.Parameters.Add("@expectedResult", SqlDbType.NVarChar, 255).Value = blackList.ExpectedResult;
        command.Parameters.Add("@score", SqlDbType.Int).Value = blackList.Score;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, System.Globalization.CultureInfo.InvariantCulture);
    }

    internal async ValueTask DeleteAllDnsBlackListsForRestoreAsync(
        CancellationToken cancellationToken)
    {
        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            DeleteAllDnsBlackListsSql,
            cancellationToken).ConfigureAwait(false);
        await commandLease.Command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> UpdateDnsBlackListAsync(
        DnsBlackListAdministrationSnapshot blackList,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blackList);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateDnsBlackListSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = blackList.Id;
        command.Parameters.Add("@active", SqlDbType.Bit).Value = blackList.Active;
        command.Parameters.Add("@dnsHost", SqlDbType.NVarChar, 255).Value = blackList.DnsHost;
        command.Parameters.Add("@rejectMessage", SqlDbType.NVarChar, 255).Value = blackList.RejectMessage;
        command.Parameters.Add("@expectedResult", SqlDbType.NVarChar, 255).Value = blackList.ExpectedResult;
        command.Parameters.Add("@score", SqlDbType.Int).Value = blackList.Score;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<bool> DeleteDnsBlackListByIdAsync(
        int databaseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteDnsBlackListSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = databaseId;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }
}
