using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapMessageMutationStore : IImapMessageMutationStore
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
        m.messagefilename,
        a.accountaddress,
        CONVERT(bigint, ROW_NUMBER() OVER (ORDER BY m.messageuid ASC)) AS sequencenumber
    FROM hm_messages AS m WITH (UPDLOCK, HOLDLOCK)
    LEFT JOIN hm_accounts AS a
        ON a.accountid = m.messageaccountid
    WHERE
        m.messagetype = 2
        AND m.messageaccountid = @AccountId
        AND m.messagefolderid = @FolderId
)
""";

    public const string UpdateFlagsSql = """
UPDATE hm_messages
SET messageflags = @Flags
WHERE
    messageid = @MessageId
    AND messageaccountid = @AccountId
    AND messagefolderid = @FolderId;

UPDATE hm_message_search_documents
SET
    messageflags = @Flags,
    updatedutc = SYSUTCDATETIME()
WHERE messageid = @MessageId;
""";

    public const string SelectEffectiveAclValueSql = """
WITH FolderChain AS
(
    SELECT folderid, folderparentid, CONVERT(int, 0) AS depth
    FROM hm_imapfolders WITH (UPDLOCK, HOLDLOCK)
    WHERE folderid = @FolderId AND folderaccountid = 0

    UNION ALL

    SELECT parent.folderid, parent.folderparentid, chain.depth + 1
    FROM FolderChain AS chain
    INNER JOIN hm_imapfolders AS parent WITH (UPDLOCK, HOLDLOCK)
        ON parent.folderid = chain.folderparentid
        AND parent.folderaccountid = 0
    WHERE chain.depth < 249
),
EffectiveFolder AS
(
    SELECT TOP (1) chain.folderid, chain.depth
    FROM FolderChain AS chain
    WHERE EXISTS
    (
        SELECT 1
        FROM hm_acl AS candidate WITH (UPDLOCK, HOLDLOCK)
        WHERE candidate.aclsharefolderid = chain.folderid
    )
    ORDER BY chain.depth ASC
)
SELECT TOP (1) acl.aclvalue
FROM EffectiveFolder AS effective
INNER JOIN hm_acl AS acl WITH (UPDLOCK, HOLDLOCK)
    ON acl.aclsharefolderid = effective.folderid
WHERE
    (acl.aclpermissiontype = 0 AND acl.aclpermissionaccountid = @RequesterAccountId)
    OR
    (acl.aclpermissiontype = 1 AND EXISTS
    (
        SELECT 1
        FROM hm_group_members AS member WITH (UPDLOCK, HOLDLOCK)
        WHERE member.membergroupid = acl.aclpermissiongroupid
          AND member.memberaccountid = @RequesterAccountId
    ))
    OR acl.aclpermissiontype = 2
ORDER BY
    CASE acl.aclpermissiontype WHEN 0 THEN 0 WHEN 1 THEN 1 ELSE 2 END,
    acl.aclid ASC;
""";

    public const string DeleteMessageSql = """
DELETE FROM hm_message_search_queue WHERE messageid = @MessageId;
DELETE FROM hm_message_search_documents WHERE messageid = @MessageId;
DELETE FROM hm_message_metadata WHERE metadata_messageid = @MessageId;
DELETE FROM hm_messages
WHERE
    messageid = @MessageId
    AND messageaccountid = @AccountId
    AND messagefolderid = @FolderId
    AND (messageflags & @DeletedFlag) = @DeletedFlag;
""";

    private const byte MutableFlags =
        ImapMessageFlags.Seen |
        ImapMessageFlags.Deleted |
        ImapMessageFlags.Flagged |
        ImapMessageFlags.Answered |
        ImapMessageFlags.Draft;

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver _pathResolver;
    private readonly Action<int>? _accountSizeInvalidationCallback;
    private readonly Func<CancellationToken, ValueTask<IDisposable>>? _enterWriter;
    private readonly bool _useAcl;

    public SqlServerImapMessageMutationStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver,
        Action<int>? accountSizeInvalidationCallback = null)
        : this(connectionFactory, pathResolver, accountSizeInvalidationCallback, enterWriter: null)
    {
    }

    public SqlServerImapMessageMutationStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver,
        Action<int>? accountSizeInvalidationCallback,
        Func<CancellationToken, ValueTask<IDisposable>>? enterWriter,
        bool useAcl = true)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
        _accountSizeInvalidationCallback = accountSizeInvalidationCallback;
        _enterWriter = enterWriter;
        _useAcl = useAcl;
    }

    public async IAsyncEnumerable<ImapStoredMessage> StoreFlagsAsync(
        ImapStoreRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = await StoreFlagsCoreAsync(request, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
        }
    }

    public async IAsyncEnumerable<ImapExpungedMessage> ExpungeDeletedAsync(
        int accountId,
        int folderId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = await ExpungeDeletedCoreAsync(accountId, folderId, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
        }
    }

    public static SqlMessageFetchPlan PlanStore(ImapStoreRequest request)
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

    public static string BuildExpungeSnapshotSql() =>
        MailboxSnapshotCte + """
SELECT
    messageid,
    messageaccountid,
    messagefolderid,
    messageuid,
    messageflags,
    messagefilename,
    accountaddress,
    sequencenumber
FROM MailboxMessages
WHERE (messageflags & @DeletedFlag) = @DeletedFlag
ORDER BY messageuid ASC;
""";

    private async ValueTask<IReadOnlyList<ImapStoredMessage>> StoreFlagsCoreAsync(
        ImapStoreRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var writerLease = _enterWriter is null
            ? null
            : await _enterWriter(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await LoadRowsAsync(connection, PlanStore(request), transaction, cancellationToken).ConfigureAwait(false);
            if (rows.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Array.Empty<ImapStoredMessage>();
            }

            await EnsureStoreAuthorizationAsync(connection, transaction, request, rows, cancellationToken).ConfigureAwait(false);
            var updated = new List<ImapStoredMessage>(rows.Count);
            foreach (var row in rows)
            {
                var flags = ApplyFlags(row, request);
                await using var command = new SqlCommand(UpdateFlagsSql, connection, transaction);
                command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = row.Identity.MessageId;
                command.Parameters.Add("@AccountId", SqlDbType.Int).Value = request.AccountId;
                command.Parameters.Add("@FolderId", SqlDbType.Int).Value = request.FolderId;
                command.Parameters.Add("@Flags", SqlDbType.TinyInt).Value = flags;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                updated.Add(new ImapStoredMessage(row.Identity, row.SequenceNumber, flags));
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return updated;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<IReadOnlyList<ImapExpungedMessage>> ExpungeDeletedCoreAsync(
        int accountId,
        int folderId,
        CancellationToken cancellationToken)
    {
        // Public folders use the legacy account-zero storage scope.
        ArgumentOutOfRangeException.ThrowIfNegative(accountId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(folderId);

        using var writerLease = _enterWriter is null
            ? null
            : await _enterWriter(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await LoadDeletedRowsAsync(connection, accountId, folderId, cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return Array.Empty<ImapExpungedMessage>();
        }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var row in rows)
            {
                await using var command = new SqlCommand(DeleteMessageSql, connection, transaction);
                command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = row.Identity.MessageId;
                command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
                command.Parameters.Add("@FolderId", SqlDbType.Int).Value = folderId;
                command.Parameters.Add("@DeletedFlag", SqlDbType.TinyInt).Value = ImapMessageFlags.Deleted;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        _accountSizeInvalidationCallback?.Invoke(accountId);

        foreach (var row in rows)
        {
            TryDeleteMessageFile(row);
        }

        var expunged = new List<ImapExpungedMessage>(rows.Count);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            expunged.Add(new ImapExpungedMessage(row.Identity, row.SequenceNumber - index));
        }

        return expunged;
    }

    private async ValueTask<IReadOnlyList<MessageMutationRow>> LoadDeletedRowsAsync(
        SqlConnection connection,
        int accountId,
        int folderId,
        CancellationToken cancellationToken)
    {
        var commandText = BuildExpungeSnapshotSql();
        await using var command = new SqlCommand(commandText, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = folderId;
        command.Parameters.Add("@DeletedFlag", SqlDbType.TinyInt).Value = ImapMessageFlags.Deleted;
        return await LoadRowsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureStoreAuthorizationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ImapStoreRequest request,
        IReadOnlyList<MessageMutationRow> rows,
        CancellationToken cancellationToken)
    {
        if (request.AccountId != 0 || !_useAcl)
        {
            return;
        }

        if (request.RequesterAccountId is not int requesterAccountId || requesterAccountId <= 0)
        {
            throw new UnauthorizedAccessException("ACL: public mailbox STORE caller identity is missing.");
        }

        var requiredRights = request.Mode == ImapStoreMode.Set
            ? 0L
            : GetRequiredStoreRights(request.Flags);
        foreach (var row in rows)
        {
            if (request.Mode == ImapStoreMode.Set)
            {
                requiredRights |= GetRequiredStoreRights(row.Flags, ApplyFlags(row, request));
            }
        }
        if (requiredRights == 0)
        {
            return;
        }

        await using var command = new SqlCommand(SelectEffectiveAclValueSql, connection, transaction);
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = request.FolderId;
        command.Parameters.Add("@RequesterAccountId", SqlDbType.Int).Value = requesterAccountId;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var aclValue = value is null ? 0L : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        if ((aclValue & requiredRights) != requiredRights)
        {
            throw new UnauthorizedAccessException("ACL: STORE permission denied.");
        }
    }

    private static long GetRequiredStoreRights(byte currentFlags, byte updatedFlags)
    {
        var flags = (byte)((currentFlags ^ updatedFlags) & MutableFlags);
        var required = 0L;
        if ((flags & ImapMessageFlags.Seen) != 0)
            required |= ImapAclRights.WriteSeen;
        if ((flags & ImapMessageFlags.Deleted) != 0)
            required |= ImapAclRights.WriteDeleted;
        if ((flags & (ImapMessageFlags.Flagged | ImapMessageFlags.Answered | ImapMessageFlags.Draft)) != 0)
            required |= ImapAclRights.WriteOthers;
        return required;
    }

    private static long GetRequiredStoreRights(byte flags) =>
        GetRequiredStoreRights(0, flags);

    private static async ValueTask<IReadOnlyList<MessageMutationRow>> LoadRowsAsync(
        SqlConnection connection,
        SqlMessageFetchPlan plan,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(plan.CommandText, connection, transaction);
        foreach (var parameter in plan.Parameters)
        {
            AddPlanParameter(command, parameter.Key, parameter.Value);
        }

        return await LoadRowsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<IReadOnlyList<MessageMutationRow>> LoadRowsAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<MessageMutationRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    private void TryDeleteMessageFile(MessageMutationRow row)
    {
        var path = _pathResolver.Resolve(
            row.MessageFileName,
            row.Identity.AccountId,
            row.Identity.FolderId,
            row.AccountAddress);

        if (path is null || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static byte ApplyFlags(MessageMutationRow row, ImapStoreRequest request) =>
        request.Mode switch
        {
            ImapStoreMode.Set => (byte)((row.Flags & ~MutableFlags) | request.Flags),
            ImapStoreMode.Add => (byte)(row.Flags | request.Flags),
            ImapStoreMode.Remove => (byte)(row.Flags & ~request.Flags),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "Unknown IMAP STORE mode.")
        };

    private static MessageMutationRow ReadRow(SqlDataReader reader) =>
        new(
            new MessageIdentity(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt64(3)),
            reader.GetByte(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetInt64(7));

    private static void AddIdRangeFilter(
        StringBuilder sql,
        IDictionary<string, object> parameters,
        string column,
        IReadOnlyList<ImapIdRange> ranges)
    {
        if (ranges.Count == 0)
        {
            throw new InvalidOperationException("STORE message set is empty.");
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
            _ => throw new NotSupportedException($"Unsupported SQL STORE parameter type {value.GetType().FullName}.")
        };

        command.Parameters.Add(parameter);
    }

    private sealed record MessageMutationRow(
        MessageIdentity Identity,
        byte Flags,
        string MessageFileName,
        string? AccountAddress,
        long SequenceNumber);
}
