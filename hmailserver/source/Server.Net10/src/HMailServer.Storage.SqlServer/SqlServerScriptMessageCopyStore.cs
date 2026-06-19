using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerScriptMessageCopyStore : IScriptMessageCopyStore
{
    public const string LoadDestinationFolderSql = """
SELECT TOP (1)
    a.accountaddress
FROM hm_imapfolders AS f
INNER JOIN hm_accounts AS a
    ON a.accountid = f.folderaccountid
WHERE
    f.folderid = @DestinationFolderId
    AND f.folderaccountid = @SourceAccountId
    AND a.accountactive <> 0;
""";

    public const string AllocateDestinationUidSql = """
UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)
SET foldercurrentuid = foldercurrentuid + 1
OUTPUT INSERTED.foldercurrentuid
WHERE
    folderid = @DestinationFolderId
    AND folderaccountid = @SourceAccountId;
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
    @SourceAccountId,
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

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver _pathResolver;

    public SqlServerScriptMessageCopyStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
    }

    public async ValueTask<MessageIdentity> CopyAsync(
        ScriptMessageCopyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.SourceAccountId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DestinationFolderId);
        ArgumentNullException.ThrowIfNull(request.MessageData);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var accountAddress = await LoadDestinationAccountAddressAsync(
            connection,
            request,
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accountAddress))
        {
            throw new InvalidOperationException("Message.Copy destination folder does not belong to the source account.");
        }

        var destinationFileName = Guid.NewGuid().ToString("N") + ".eml";
        var destinationPath = _pathResolver.Resolve(
            destinationFileName,
            request.SourceAccountId,
            request.DestinationFolderId,
            accountAddress);
        if (destinationPath is null)
        {
            throw new IOException("Message.Copy destination path is invalid.");
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        try
        {
            await WriteSnapshotAsync(destinationPath, request.MessageData, cancellationToken).ConfigureAwait(false);
            await using var transaction = (SqlTransaction)await connection
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                var uid = await AllocateDestinationUidAsync(
                    connection,
                    transaction,
                    request,
                    cancellationToken).ConfigureAwait(false);
                var messageId = await InsertCopiedMessageAsync(
                    connection,
                    transaction,
                    request,
                    destinationFileName,
                    uid,
                    cancellationToken).ConfigureAwait(false);
                await QueueForIndexingAsync(
                    connection,
                    transaction,
                    messageId,
                    cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new MessageIdentity(
                    messageId,
                    request.SourceAccountId,
                    request.DestinationFolderId,
                    uid);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            TryDelete(destinationPath);
            throw;
        }
    }

    private static async ValueTask<string?> LoadDestinationAccountAddressAsync(
        SqlConnection connection,
        ScriptMessageCopyRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(LoadDestinationFolderSql, connection);
        command.Parameters.Add("@SourceAccountId", SqlDbType.Int).Value = request.SourceAccountId;
        command.Parameters.Add("@DestinationFolderId", SqlDbType.Int).Value = request.DestinationFolderId;
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    private static async ValueTask<long> AllocateDestinationUidAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ScriptMessageCopyRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(AllocateDestinationUidSql, connection, transaction);
        command.Parameters.Add("@SourceAccountId", SqlDbType.Int).Value = request.SourceAccountId;
        command.Parameters.Add("@DestinationFolderId", SqlDbType.Int).Value = request.DestinationFolderId;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null || result is DBNull)
        {
            throw new InvalidOperationException("Message.Copy could not allocate a destination UID.");
        }

        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async ValueTask<long> InsertCopiedMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ScriptMessageCopyRequest request,
        string destinationFileName,
        long uid,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertCopiedMessageSql, connection, transaction);
        command.Parameters.Add("@SourceAccountId", SqlDbType.Int).Value = request.SourceAccountId;
        command.Parameters.Add("@DestinationFolderId", SqlDbType.Int).Value = request.DestinationFolderId;
        command.Parameters.Add("@MessageFileName", SqlDbType.NVarChar, 255).Value = destinationFileName;
        command.Parameters.Add("@MessageFrom", SqlDbType.NVarChar, 255).Value = request.FromAddress;
        command.Parameters.Add("@MessageSize", SqlDbType.BigInt).Value = request.MessageData.LongLength;
        command.Parameters.Add("@MessageFlags", SqlDbType.TinyInt).Value = (byte)(request.Flags | ImapMessageFlags.Recent);
        command.Parameters.Add("@MessageCreateTime", SqlDbType.DateTime).Value = request.CreatedUtc.UtcDateTime;
        command.Parameters.Add("@MessageUid", SqlDbType.BigInt).Value = uid;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null || result is DBNull)
        {
            throw new InvalidOperationException("Message.Copy could not insert the destination message.");
        }

        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
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

    private static async ValueTask WriteSnapshotAsync(
        string destinationPath,
        byte[] messageData,
        CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await destination.WriteAsync(messageData, cancellationToken).ConfigureAwait(false);
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
}
