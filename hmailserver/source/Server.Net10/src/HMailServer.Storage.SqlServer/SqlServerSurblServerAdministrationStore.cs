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

    public const string InsertSurblServerSql = """
INSERT INTO hm_surblservers
    (surblactive, surblhost, surblrejectmessage, surblscore)
OUTPUT INSERTED.surblid
VALUES (@active, @dnsHost, @rejectMessage, @score);
""";

    public const string UpdateSurblServerSql = """
UPDATE hm_surblservers
SET surblactive = @active,
    surblhost = @dnsHost,
    surblrejectmessage = @rejectMessage,
    surblscore = @score
WHERE surblid = @id;
""";

    public const string DeleteSurblServerSql = """
DELETE FROM hm_surblservers
WHERE surblid = @id;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerSurblServerAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerSurblServerAdministrationStore(
        SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _connectionFactory = null!;
        _transactionContext = transactionContext;
    }

    public async ValueTask<IReadOnlyList<SurblServerAdministrationSnapshot>> GetSurblServersAsync(
        CancellationToken cancellationToken)
    {
        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            GetSurblServersSql,
            cancellationToken).ConfigureAwait(false);
        var command = commandLease.Command;
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

    public async ValueTask<int> InsertSurblServerAsync(
        SurblServerAdministrationSnapshot server,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertSurblServerSql, connection);
        command.Parameters.Add("@active", SqlDbType.Bit).Value = server.Active;
        command.Parameters.Add("@dnsHost", SqlDbType.NVarChar, 255).Value = server.DnsHost;
        command.Parameters.Add("@rejectMessage", SqlDbType.NVarChar, 255).Value = server.RejectMessage;
        command.Parameters.Add("@score", SqlDbType.Int).Value = server.Score;

        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask<bool> UpdateSurblServerAsync(
        SurblServerAdministrationSnapshot server,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateSurblServerSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = server.Id;
        command.Parameters.Add("@active", SqlDbType.Bit).Value = server.Active;
        command.Parameters.Add("@dnsHost", SqlDbType.NVarChar, 255).Value = server.DnsHost;
        command.Parameters.Add("@rejectMessage", SqlDbType.NVarChar, 255).Value = server.RejectMessage;
        command.Parameters.Add("@score", SqlDbType.Int).Value = server.Score;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<bool> DeleteSurblServerByIdAsync(
        int databaseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteSurblServerSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = databaseId;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }
}
