using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapMessageFetchStore : IImapMessageFetchStore
{
    public const string MailboxSnapshotCte = """
WITH MailboxMessages AS
(
    SELECT
        m.messageid,
        m.messageaccountid,
        m.messagefolderid,
        m.messageuid,
        m.messageflags,
        m.messagesize,
        m.messagecreatetime,
        m.messagefilename,
        a.accountaddress,
        CONVERT(bigint, ROW_NUMBER() OVER (ORDER BY m.messageuid ASC)) AS sequencenumber
    FROM hm_messages AS m WITH (READCOMMITTEDLOCK)
    LEFT JOIN hm_accounts AS a
        ON a.accountid = m.messageaccountid
    WHERE
        m.messagetype = 2
        AND m.messageaccountid = @AccountId
        AND m.messagefolderid = @FolderId
)
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver _pathResolver;

    public SqlServerImapMessageFetchStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
    }

    public async IAsyncEnumerable<ImapFetchedMessage> FetchAsync(
        ImapFetchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequiresRawMessage)
        {
            var rows = await LoadRowsAsync(request, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return row.ToFetchedMessage(await ReadRawMessageAsync(row, cancellationToken).ConfigureAwait(false));
            }

            yield break;
        }

        await foreach (var row in StreamRowsAsync(request, cancellationToken).ConfigureAwait(false))
        {
            yield return row.ToFetchedMessage(rawMessage: null);
        }
    }

    public static SqlMessageFetchPlan Plan(ImapFetchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sql = new StringBuilder();
        sql.AppendLine(MailboxSnapshotCte);
        sql.AppendLine("""
SELECT
    messageid,
    messageaccountid,
    messagefolderid,
    messageuid,
    messageflags,
    messagesize,
    messagecreatetime,
    messagefilename,
    accountaddress,
    sequencenumber
FROM MailboxMessages
""");

        var parameters = new Dictionary<string, object>
        {
            ["@AccountId"] = request.AccountId,
            ["@FolderId"] = request.FolderId
        };

        sql.Append("WHERE ");
        AddIdRangeFilter(
            sql,
            parameters,
            request.UseUid ? "messageuid" : "sequencenumber",
            request.MessageSet);
        sql.AppendLine();
        sql.AppendLine("ORDER BY messageuid ASC;");
        return new SqlMessageFetchPlan(sql.ToString(), parameters);
    }

    private async ValueTask<IReadOnlyList<MessageFetchRow>> LoadRowsAsync(
        ImapFetchRequest request,
        CancellationToken cancellationToken)
    {
        var rows = new List<MessageFetchRow>();
        await foreach (var row in StreamRowsAsync(request, cancellationToken).ConfigureAwait(false))
        {
            rows.Add(row);
        }

        return rows;
    }

    private async IAsyncEnumerable<MessageFetchRow> StreamRowsAsync(
        ImapFetchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var plan = Plan(request);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(plan.CommandText, connection);

        foreach (var parameter in plan.Parameters)
        {
            AddPlanParameter(command, parameter.Key, parameter.Value);
        }

        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return ReadRow(reader);
        }
    }

    private async ValueTask<byte[]> ReadRawMessageAsync(
        MessageFetchRow row,
        CancellationToken cancellationToken)
    {
        var path = _pathResolver.Resolve(
            row.MessageFileName,
            row.Identity.AccountId,
            row.Identity.FolderId,
            row.AccountAddress);

        if (path is null || !File.Exists(path))
        {
            throw new IOException("Message content file is missing.");
        }

        return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static MessageFetchRow ReadRow(SqlDataReader reader)
    {
        var createTime = DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc);
        return new MessageFetchRow(
            new MessageIdentity(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt64(3)),
            reader.GetByte(4),
            reader.GetInt64(5),
            new DateTimeOffset(createTime),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetInt64(9));
    }

    private static void AddIdRangeFilter(
        StringBuilder sql,
        IDictionary<string, object> parameters,
        string column,
        IReadOnlyList<ImapIdRange> ranges)
    {
        if (ranges.Count == 0)
        {
            throw new InvalidOperationException("FETCH message set is empty.");
        }

        sql.Append('(');
        for (var index = 0; index < ranges.Count; index++)
        {
            if (index > 0)
            {
                sql.Append(" OR ");
            }

            var range = ranges[index];
            if (range.Start is null && range.End is null)
            {
                sql.Append("1 = 1");
                continue;
            }

            if (range.Start is null)
            {
                var endOnlyName = $"@RangeEnd{index}";
                parameters[endOnlyName] = range.End!.Value;
                sql.Append(column).Append(" <= ").Append(endOnlyName);
                continue;
            }

            var startName = $"@RangeStart{index}";
            parameters[startName] = range.Start.Value;

            if (range.End is null)
            {
                sql.Append(column).Append(" >= ").Append(startName);
                continue;
            }

            if (range.IsSingle)
            {
                sql.Append(column).Append(" = ").Append(startName);
                continue;
            }

            var endName = $"@RangeEnd{index}";
            parameters[endName] = range.End.Value;
            sql.Append(column)
                .Append(" BETWEEN ")
                .Append(startName)
                .Append(" AND ")
                .Append(endName);
        }

        sql.Append(')');
    }

    private static void AddPlanParameter(SqlCommand command, string name, object value)
    {
        var parameter = value switch
        {
            int typed => new SqlParameter(name, SqlDbType.Int) { Value = typed },
            long typed => new SqlParameter(name, SqlDbType.BigInt) { Value = typed },
            _ => throw new NotSupportedException($"Unsupported SQL fetch parameter type {value.GetType().FullName}.")
        };

        command.Parameters.Add(parameter);
    }

    private sealed record MessageFetchRow(
        MessageIdentity Identity,
        byte Flags,
        long SizeBytes,
        DateTimeOffset InternalDateUtc,
        string MessageFileName,
        string? AccountAddress,
        long SequenceNumber)
    {
        public ImapFetchedMessage ToFetchedMessage(byte[]? rawMessage) =>
            new(
                Identity,
                SequenceNumber,
                Flags,
                SizeBytes,
                InternalDateUtc,
                rawMessage);
    }
}
