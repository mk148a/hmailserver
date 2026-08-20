using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerMessageAdministrationStore : IMessageAdministrationStore, IMessageAdministrationRestoreStore
{
    public const string GetAccountMessagesSql = """
SELECT
    messageid,
    messageaccountid,
    messagefolderid,
    messagefilename,
    messagetype,
    messagefrom,
    messagesize,
    messagecurnooftries,
    messageflags,
    messagecreatetime,
    messageuid
FROM hm_messages
WHERE messageaccountid = @AccountID
ORDER BY messageuid ASC;
""";

    public const string GetFolderMessagesSql = """
SELECT
    messageid,
    messageaccountid,
    messagefolderid,
    messagefilename,
    messagetype,
    messagefrom,
    messagesize,
    messagecurnooftries,
    messageflags,
    messagecreatetime,
    messageuid
FROM hm_messages
WHERE messageaccountid = @AccountID
  AND messagefolderid = @FolderID
  AND messagetype = 2
  AND EXISTS
  (
      SELECT 1
      FROM hm_imapfolders
      WHERE folderid = @FolderID
        AND folderaccountid = @AccountID
  )
ORDER BY messageuid ASC, messageid ASC;
""";

    public const string InsertMessageSql = """
        INSERT INTO hm_messages
            (messageaccountid, messagefolderid, messagefilename, messagetype, messagefrom,
             messagesize, messagecurnooftries, messagenexttrytime, messageflags,
             messagecreatetime, messagelocked, messageuid)
        OUTPUT INSERTED.messageid
        SELECT
            @AccountID, @FolderID, @FileName, @State, @From,
            @Size, @CurrentNumberOfTries, @NextTryTime, @Flags,
            @CreateTime, @Locked, @Uid
        FROM hm_imapfolders WITH (UPDLOCK, HOLDLOCK)
        WHERE folderid = @FolderID
          AND folderaccountid = @AccountID;
        """;

    public const string AllocateFolderUidSql = """
        UPDATE hm_imapfolders
        SET foldercurrentuid = foldercurrentuid + 1
        OUTPUT INSERTED.foldercurrentuid
        WHERE folderid = @FolderID
          AND folderaccountid = @AccountID;
        """;

    private const int DeliveredState = 2;

    public const string UpdateMessageSql = """
        UPDATE hm_messages
        SET messagefolderid = @FolderID,
            messagefilename = @FileName,
            messagefrom = @From,
            messagesize = @Size,
            messageflags = @Flags,
            messagecreatetime = @CreateTime,
            messageuid = @Uid
        WHERE messageid = @MessageID
          AND messageaccountid = @AccountID
          AND messagefolderid = @FolderID;
        """;    public const string DeleteMessageSql = """
        DELETE FROM hm_messages
        WHERE messageid = @MessageID
          AND messageaccountid = @AccountID
          AND messagefolderid = @FolderID;
        """;

    public const string ClearMessagesSql = """
        DELETE FROM hm_messages
        WHERE messageaccountid = @AccountID AND messagefolderid = @FolderID;
        """;

    public const string InsertMessageForRestoreSql = """
INSERT INTO hm_messages
    (messageaccountid, messagefolderid, messagefilename, messagetype, messagefrom,
     messagesize, messagecurnooftries, messagenexttrytime, messageflags,
     messagecreatetime, messagelocked, messageuid)
OUTPUT INSERTED.messageid
SELECT @AccountID, @FolderID, @FileName, @State, @From,
       @Size, @CurrentNumberOfTries, CONVERT(datetime, '1901-01-01', 120), @Flags,
       @CreateTime, 0, @Uid
WHERE EXISTS
(
    SELECT 1 FROM hm_imapfolders
    WHERE folderid = @FolderID AND folderaccountid = @AccountID
);
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerMessageAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerMessageAdministrationStore(SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _connectionFactory = null!;
        _transactionContext = transactionContext;
    }

    public async ValueTask<MessageAdministrationInsertResult> InsertMessageForRestoreAsync(
        int accountId,
        int folderId,
        MessageAdministrationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            InsertMessageForRestoreSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folderId;
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = snapshot.FileName;
        command.Parameters.Add("@State", SqlDbType.TinyInt).Value = snapshot.State;
        command.Parameters.Add("@From", SqlDbType.NVarChar, 255).Value = snapshot.FromAddress;
        command.Parameters.Add("@Size", SqlDbType.BigInt).Value = snapshot.SizeBytes;
        command.Parameters.Add("@CurrentNumberOfTries", SqlDbType.Int).Value = snapshot.CurrentNumberOfTries;
        command.Parameters.Add("@Flags", SqlDbType.TinyInt).Value = snapshot.Flags;
        command.Parameters.Add("@CreateTime", SqlDbType.DateTime).Value = snapshot.InternalDate;
        command.Parameters.Add("@Uid", SqlDbType.BigInt).Value = snapshot.Uid;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (insertedId is null || insertedId == DBNull.Value)
        {
            throw new InvalidOperationException("The restored message insert did not return a generated identity.");
        }

        return new MessageAdministrationInsertResult(
            Convert.ToInt64(insertedId, CultureInfo.InvariantCulture), snapshot.Uid, snapshot.State);
    }


    public async ValueTask<bool> DeleteMessageAsync(
        int accountId,
        int folderId,
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteMessageSql, connection);
        command.Parameters.Add("@MessageID", SqlDbType.BigInt).Value = messageId;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folderId;
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }

    public async ValueTask ClearMessagesAsync(
        int accountId,
        int folderId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(ClearMessagesSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folderId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> UpdateMessageAsync(
        int accountId,
        int folderId,
        MessageAdministrationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateMessageSql, connection);
        command.Parameters.Add("@MessageID", SqlDbType.BigInt).Value = snapshot.Id;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folderId;
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = snapshot.FileName;
        command.Parameters.Add("@From", SqlDbType.NVarChar, 255).Value = snapshot.FromAddress;
        command.Parameters.Add("@Size", SqlDbType.BigInt).Value = snapshot.SizeBytes;
        command.Parameters.Add("@Flags", SqlDbType.TinyInt).Value = snapshot.Flags;
        command.Parameters.Add("@CreateTime", SqlDbType.DateTime).Value = snapshot.InternalDate;
        command.Parameters.Add("@Uid", SqlDbType.BigInt).Value = snapshot.Uid;
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }
    public async ValueTask<MessageAdministrationInsertResult> InsertMessageAsync(
        int accountId,
        int folderId,
        MessageAdministrationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var allocateCommand = new SqlCommand(AllocateFolderUidSql, connection, transaction);
        allocateCommand.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        allocateCommand.Parameters.Add("@FolderID", SqlDbType.Int).Value = folderId;
        var allocatedUid = await allocateCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (allocatedUid is null || allocatedUid == DBNull.Value)
        {
            throw new InvalidOperationException("The message folder does not exist for the selected account.");
        }

        await using var command = new SqlCommand(InsertMessageSql, connection, transaction);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folderId;
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = snapshot.FileName;
        command.Parameters.Add("@State", SqlDbType.TinyInt).Value = DeliveredState;
        command.Parameters.Add("@From", SqlDbType.NVarChar, 255).Value = snapshot.FromAddress;
        command.Parameters.Add("@Size", SqlDbType.BigInt).Value = snapshot.SizeBytes;
        command.Parameters.Add("@CurrentNumberOfTries", SqlDbType.Int).Value = snapshot.CurrentNumberOfTries;
        command.Parameters.Add("@NextTryTime", SqlDbType.DateTime).Value = snapshot.InternalDate;
        command.Parameters.Add("@Flags", SqlDbType.TinyInt).Value = snapshot.Flags;
        command.Parameters.Add("@CreateTime", SqlDbType.DateTime).Value = snapshot.InternalDate;
        command.Parameters.Add("@Locked", SqlDbType.TinyInt).Value = 0;
        command.Parameters.Add("@Uid", SqlDbType.BigInt).Value = allocatedUid;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (insertedId is null || insertedId == DBNull.Value)
        {
            throw new InvalidOperationException("The message insert did not return a generated identity.");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new MessageAdministrationInsertResult(
            Convert.ToInt64(insertedId, CultureInfo.InvariantCulture),
            Convert.ToInt64(allocatedUid, CultureInfo.InvariantCulture),
            DeliveredState);
    }
    public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
        int accountId,
        CancellationToken cancellationToken) =>
        GetMessagesAsync(GetAccountMessagesSql, cancellationToken, ("@AccountID", accountId));

    public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
        int accountId,
        int folderId,
        CancellationToken cancellationToken) =>
        GetMessagesAsync(
            GetFolderMessagesSql,
            cancellationToken,
            ("@AccountID", accountId),
            ("@FolderID", folderId));

    private async ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetMessagesAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, int Value)[] parameters)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter.Name, SqlDbType.Int).Value = parameter.Value;
        }
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var messages = new List<MessageAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messages.Add(
                new MessageAdministrationSnapshot(
                    Id: Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    AccountId: Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                    FolderId: Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    FileName: reader.GetString(3),
                    State: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    FromAddress: reader.GetString(5),
                    SizeBytes: Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture),
                    CurrentNumberOfTries: Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture),
                    Flags: Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture),
                    InternalDate: reader.GetDateTime(9),
                    Uid: Convert.ToInt64(reader.GetValue(10), CultureInfo.InvariantCulture)));
        }

        return messages;
    }
}
