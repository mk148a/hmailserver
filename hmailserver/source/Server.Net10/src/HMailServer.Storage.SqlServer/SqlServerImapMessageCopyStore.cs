using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapMessageCopyStore : IImapMessageCopyStore
{
    public const string SourceSnapshotCte = """
WITH SourceMessages AS
(
    SELECT
        m.messageid,
        m.messageaccountid,
        m.messagefolderid,
        m.messageuid,
        m.messageflags,
        m.messagefilename,
        m.messagefrom,
        m.messagesize,
        m.messagecreatetime,
        a.accountaddress,
        CONVERT(bigint, ROW_NUMBER() OVER (ORDER BY m.messageuid ASC)) AS sequencenumber
    FROM hm_messages AS m WITH (READCOMMITTEDLOCK)
    LEFT JOIN hm_accounts AS a
        ON a.accountid = m.messageaccountid
    WHERE
        m.messagetype = 2
        AND m.messageaccountid = @SourceAccountId
        AND m.messagefolderid = @SourceFolderId
)
""";

    public const string AllocateUidSql = """
UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)
SET foldercurrentuid = foldercurrentuid + 1
OUTPUT INSERTED.foldercurrentuid
WHERE
    folderaccountid = @DestinationAccountId
    AND folderid = @DestinationFolderId;
""";

    public const string InsertCopiedMessageSql = """
INSERT INTO hm_messages
(
    messageaccountid,
    messagefolderid,
    messagefilename,
    messagetype,
    messagefrom,
    messagesize,
    messagecurnooftries,
    messagenexttrytime,
    messageflags,
    messagecreatetime,
    messagelocked,
    messageuid
)
OUTPUT INSERTED.messageid
VALUES
(
    @DestinationAccountId,
    @DestinationFolderId,
    @MessageFileName,
    2,
    @MessageFrom,
    @MessageSize,
    0,
    CONVERT(datetime, '1901-01-01', 120),
    @MessageFlags,
    @MessageCreateTime,
    0,
    @MessageUid
);
""";

    public const string QueueCopiedMessageForIndexingSql = """
INSERT INTO hm_message_search_queue
(
    messageid,
    queuedutc,
    attempts,
    lastattemptutc,
    nextattemptutc,
    searchleaseowner,
    searchleaseexpiresutc,
    lasterror
)
VALUES
(
    @MessageId,
    SYSUTCDATETIME(),
    0,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL
);
""";

    public const string DeleteSourceMessageSql = """
DELETE FROM hm_message_search_queue WHERE messageid = @MessageId;
DELETE FROM hm_message_search_documents WHERE messageid = @MessageId;
DELETE FROM hm_message_metadata WHERE metadata_messageid = @MessageId;
DELETE FROM hm_messages
WHERE
    messageid = @MessageId
    AND messageaccountid = @SourceAccountId
    AND messagefolderid = @SourceFolderId;
""";

    private const string LoadAccountAddressSql = """
SELECT TOP (1) accountaddress
FROM hm_accounts
WHERE accountid = @AccountId;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver _pathResolver;

    public SqlServerImapMessageCopyStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
    }

    public async IAsyncEnumerable<ImapCopiedMessage> CopyAsync(
        ImapCopyRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = await CopyCoreAsync(request, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
        }
    }

    public static SqlMessageFetchPlan PlanCopy(ImapCopyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sql = new StringBuilder();
        sql.AppendLine(SourceSnapshotCte);
        sql.AppendLine("""
SELECT
    messageid,
    messageaccountid,
    messagefolderid,
    messageuid,
    messageflags,
    messagefilename,
    messagefrom,
    messagesize,
    messagecreatetime,
    accountaddress,
    sequencenumber
FROM SourceMessages
""");

        var parameters = new Dictionary<string, object>
        {
            ["@SourceAccountId"] = request.SourceAccountId,
            ["@SourceFolderId"] = request.SourceFolderId
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

    private async ValueTask<IReadOnlyList<ImapCopiedMessage>> CopyCoreAsync(
        ImapCopyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.DestinationAccountId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.SourceFolderId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DestinationFolderId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sourceRows = await LoadRowsAsync(connection, PlanCopy(request), cancellationToken).ConfigureAwait(false);
        if (sourceRows.Count == 0)
        {
            return Array.Empty<ImapCopiedMessage>();
        }

        var destinationAccountAddress = await LoadDestinationAccountAddressAsync(
            connection,
            request.DestinationAccountId,
            cancellationToken).ConfigureAwait(false);

        if (request.DestinationAccountId > 0 && string.IsNullOrWhiteSpace(destinationAccountAddress))
        {
            throw new InvalidOperationException("Destination account could not be found.");
        }

        var workItems = PrepareCopies(request, sourceRows, destinationAccountAddress);
        try
        {
            CopyMessageFiles(workItems);
            return await InsertCopiesAsync(connection, request, workItems, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            DeleteCopiedFiles(workItems);
            throw;
        }
    }

    private IReadOnlyList<CopyWorkItem> PrepareCopies(
        ImapCopyRequest request,
        IReadOnlyList<CopySourceRow> sourceRows,
        string? destinationAccountAddress)
    {
        var workItems = new List<CopyWorkItem>(sourceRows.Count);
        foreach (var source in sourceRows)
        {
            var sourcePath = _pathResolver.Resolve(
                source.MessageFileName,
                source.Identity.AccountId,
                source.Identity.FolderId,
                source.AccountAddress);
            if (sourcePath is null || !File.Exists(sourcePath))
            {
                throw new IOException("Source message content file is missing.");
            }

            var destinationFileName = Guid.NewGuid().ToString("N") + ".eml";
            var destinationPath = _pathResolver.Resolve(
                destinationFileName,
                request.DestinationAccountId,
                request.DestinationFolderId,
                destinationAccountAddress);
            if (destinationPath is null)
            {
                throw new IOException("Destination message path is invalid.");
            }

            workItems.Add(new CopyWorkItem(
                source,
                sourcePath,
                destinationFileName,
                destinationPath));
        }

        return workItems;
    }

    private async ValueTask<IReadOnlyList<ImapCopiedMessage>> InsertCopiesAsync(
        SqlConnection connection,
        ImapCopyRequest request,
        IReadOnlyList<CopyWorkItem> workItems,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var copied = new List<ImapCopiedMessage>(workItems.Count);

        try
        {
            for (var index = 0; index < workItems.Count; index++)
            {
                var item = workItems[index];
                var destinationUid = await AllocateDestinationUidAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
                var destinationFlags = (byte)(item.Source.Flags | ImapMessageFlags.Recent);
                var destinationMessageId = await InsertCopiedMessageAsync(
                    connection,
                    transaction,
                    request,
                    item,
                    destinationUid,
                    destinationFlags,
                    cancellationToken).ConfigureAwait(false);

                await QueueForIndexingAsync(connection, transaction, destinationMessageId, cancellationToken).ConfigureAwait(false);

                if (request.DeleteSource)
                {
                    await DeleteSourceMessageAsync(connection, transaction, request, item.Source.Identity.MessageId, cancellationToken).ConfigureAwait(false);
                }

                var destinationIdentity = new MessageIdentity(
                    destinationMessageId,
                    request.DestinationAccountId,
                    request.DestinationFolderId,
                    destinationUid);
                copied.Add(new ImapCopiedMessage(
                    item.Source.Identity,
                    item.Source.SequenceNumber,
                    destinationIdentity,
                    request.DeleteSource ? item.Source.SequenceNumber - index : null));
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (request.DeleteSource)
        {
            DeleteSourceFiles(workItems);
        }

        return copied;
    }

    private async ValueTask<string?> LoadDestinationAccountAddressAsync(
        SqlConnection connection,
        int destinationAccountId,
        CancellationToken cancellationToken)
    {
        if (destinationAccountId == 0)
        {
            return null;
        }

        await using var command = new SqlCommand(LoadAccountAddressSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = destinationAccountId;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    private static void CopyMessageFiles(IReadOnlyList<CopyWorkItem> workItems)
    {
        foreach (var item in workItems)
        {
            var destinationDirectory = Path.GetDirectoryName(item.DestinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(item.SourcePath, item.DestinationPath, overwrite: false);
        }
    }

    private static void DeleteCopiedFiles(IReadOnlyList<CopyWorkItem> workItems)
    {
        foreach (var item in workItems)
        {
            TryDelete(item.DestinationPath);
        }
    }

    private static void DeleteSourceFiles(IReadOnlyList<CopyWorkItem> workItems)
    {
        foreach (var item in workItems)
        {
            TryDelete(item.SourcePath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async ValueTask<long> AllocateDestinationUidAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ImapCopyRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(AllocateUidSql, connection, transaction);
        command.Parameters.Add("@DestinationAccountId", SqlDbType.Int).Value = request.DestinationAccountId;
        command.Parameters.Add("@DestinationFolderId", SqlDbType.Int).Value = request.DestinationFolderId;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is not long uid)
        {
            throw new InvalidOperationException("Failed to allocate destination message UID.");
        }

        return uid;
    }

    private static async ValueTask<long> InsertCopiedMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ImapCopyRequest request,
        CopyWorkItem item,
        long destinationUid,
        byte destinationFlags,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertCopiedMessageSql, connection, transaction);
        command.Parameters.Add("@DestinationAccountId", SqlDbType.Int).Value = request.DestinationAccountId;
        command.Parameters.Add("@DestinationFolderId", SqlDbType.Int).Value = request.DestinationFolderId;
        command.Parameters.Add("@MessageFileName", SqlDbType.NVarChar, 255).Value = item.DestinationFileName;
        command.Parameters.Add("@MessageFrom", SqlDbType.NVarChar, 255).Value = item.Source.FromAddress;
        command.Parameters.Add("@MessageSize", SqlDbType.BigInt).Value = item.Source.SizeBytes;
        command.Parameters.Add("@MessageFlags", SqlDbType.TinyInt).Value = destinationFlags;
        command.Parameters.Add("@MessageCreateTime", SqlDbType.DateTime).Value = item.Source.CreateTimeUtc.UtcDateTime;
        command.Parameters.Add("@MessageUid", SqlDbType.BigInt).Value = destinationUid;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is decimal numeric
            ? decimal.ToInt64(numeric)
            : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async ValueTask QueueForIndexingAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(QueueCopiedMessageForIndexingSql, connection, transaction);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask DeleteSourceMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ImapCopyRequest request,
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(DeleteSourceMessageSql, connection, transaction);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        command.Parameters.Add("@SourceAccountId", SqlDbType.Int).Value = request.SourceAccountId;
        command.Parameters.Add("@SourceFolderId", SqlDbType.Int).Value = request.SourceFolderId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<IReadOnlyList<CopySourceRow>> LoadRowsAsync(
        SqlConnection connection,
        SqlMessageFetchPlan plan,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(plan.CommandText, connection);
        foreach (var parameter in plan.Parameters)
        {
            AddPlanParameter(command, parameter.Key, parameter.Value);
        }

        var rows = new List<CopySourceRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    private static CopySourceRow ReadRow(SqlDataReader reader)
    {
        var createTime = DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc);
        return new CopySourceRow(
            new MessageIdentity(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt64(3)),
            reader.GetByte(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetInt64(7),
            new DateTimeOffset(createTime),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetInt64(10));
    }

    private static void AddIdRangeFilter(
        StringBuilder sql,
        IDictionary<string, object> parameters,
        string column,
        IReadOnlyList<ImapIdRange> ranges)
    {
        if (ranges.Count == 0)
        {
            throw new InvalidOperationException("COPY/MOVE message set is empty.");
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
            _ => throw new NotSupportedException($"Unsupported SQL COPY/MOVE parameter type {value.GetType().FullName}.")
        };

        command.Parameters.Add(parameter);
    }

    private sealed record CopySourceRow(
        MessageIdentity Identity,
        byte Flags,
        string MessageFileName,
        string FromAddress,
        long SizeBytes,
        DateTimeOffset CreateTimeUtc,
        string? AccountAddress,
        long SequenceNumber);

    private sealed record CopyWorkItem(
        CopySourceRow Source,
        string SourcePath,
        string DestinationFileName,
        string DestinationPath);
}
