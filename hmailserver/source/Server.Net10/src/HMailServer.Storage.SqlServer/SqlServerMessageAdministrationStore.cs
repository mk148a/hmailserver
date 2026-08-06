using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerMessageAdministrationStore : IMessageAdministrationStore
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
WHERE messagefolderid = @FolderID
  AND messagetype = 2
ORDER BY messageuid ASC, messageid ASC;
""";

    public const string InsertMessageSql = """
        INSERT INTO hm_messages
            (messageaccountid, messagefolderid, messagefilename, messagetype, messagefrom,
             messagesize, messagecurnooftries, messagenexttrytime, messageflags,
             messagecreatetime, messagelocked, messageuid)
        OUTPUT INSERTED.messageid
        VALUES
            (@AccountID, @FolderID, @FileName, @State, @From,
             @Size, @CurrentNumberOfTries, @NextTryTime, @Flags,
             @CreateTime, @Locked, @Uid);
        """;    public const string UpdateMessageSql = """
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

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerMessageAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
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
    public async ValueTask<long> InsertMessageAsync(
        int accountId,
        int folderId,
        MessageAdministrationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertMessageSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderID", SqlDbType.Int).Value = folderId;
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = snapshot.FileName;
        command.Parameters.Add("@State", SqlDbType.TinyInt).Value = snapshot.State;
        command.Parameters.Add("@From", SqlDbType.NVarChar, 255).Value = snapshot.FromAddress;
        command.Parameters.Add("@Size", SqlDbType.BigInt).Value = snapshot.SizeBytes;
        command.Parameters.Add("@CurrentNumberOfTries", SqlDbType.Int).Value = snapshot.CurrentNumberOfTries;
        command.Parameters.Add("@NextTryTime", SqlDbType.DateTime).Value = snapshot.InternalDate;
        command.Parameters.Add("@Flags", SqlDbType.TinyInt).Value = snapshot.Flags;
        command.Parameters.Add("@CreateTime", SqlDbType.DateTime).Value = snapshot.InternalDate;
        command.Parameters.Add("@Locked", SqlDbType.TinyInt).Value = 0;
        command.Parameters.Add("@Uid", SqlDbType.BigInt).Value = snapshot.Uid;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(insertedId, CultureInfo.InvariantCulture);
    }
    public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
        int accountId,
        CancellationToken cancellationToken) =>
        GetMessagesAsync(GetAccountMessagesSql, "@AccountID", accountId, cancellationToken);

    public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
        int folderId,
        CancellationToken cancellationToken) =>
        GetMessagesAsync(GetFolderMessagesSql, "@FolderID", folderId, cancellationToken);

    private async ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetMessagesAsync(
        string sql,
        string parameterName,
        int parameterValue,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(parameterName, SqlDbType.Int).Value = parameterValue;
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
