using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerLocalDeliveryStore : ILocalDeliveryStore
{
    public const string LoadAccountAddressSql = """
SELECT TOP (1) accountaddress
FROM hm_accounts
WHERE accountid = @AccountId
  AND accountactive <> 0;
""";

    public const string AllocateInboxUidSql = """
UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)
SET foldercurrentuid = foldercurrentuid + 1
OUTPUT INSERTED.folderid, INSERTED.foldercurrentuid
WHERE
    folderaccountid = @AccountId
    AND folderparentid = -1
    AND LOWER(foldername) = 'inbox';
""";

    public const string InsertDeliveredMessageSql = """
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
    @AccountId,
    @FolderId,
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

    public const string QueueDeliveredMessageForIndexingSql = """
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

    public SqlServerLocalDeliveryStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
    }

    public async ValueTask<LocalDeliveryResult> DeliverAsync(
        DeliveryQueuedMessage message,
        DeliveryTargetBatch targetBatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(targetBatch);
        if (targetBatch.Target.Kind != DeliveryTargetKind.LocalAccount)
        {
            throw new InvalidOperationException("Local delivery requires a local account target.");
        }

        var accountId = targetBatch.Target.LocalAccountId;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var accountAddress = await LoadAccountAddressAsync(connection, accountId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accountAddress))
        {
            throw new InvalidOperationException("Local delivery account could not be found.");
        }

        var sourcePath = _pathResolver.Resolve(
            message.FileName,
            accountId: 0,
            folderId: 0,
            accountAddress: null);
        if (sourcePath is null || !File.Exists(sourcePath))
        {
            throw new IOException("Queued message file could not be found.");
        }

        var destinationFileName = Guid.NewGuid().ToString("N") + ".eml";
        var destinationPath = _pathResolver.Resolve(
            destinationFileName,
            accountId,
            folderId: 0,
            accountAddress);
        if (destinationPath is null)
        {
            throw new IOException("Destination message path is invalid.");
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await CopyFileAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false);

        try
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var allocation = await AllocateInboxUidAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);
                var messageId = await InsertDeliveredMessageAsync(
                    connection,
                    transaction,
                    message,
                    accountId,
                    allocation.FolderId,
                    destinationFileName,
                    allocation.Uid,
                    cancellationToken).ConfigureAwait(false);
                await QueueForIndexingAsync(connection, transaction, messageId, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                return new LocalDeliveryResult(
                    new MessageIdentity(messageId, accountId, allocation.FolderId, allocation.Uid),
                    targetBatch.Recipients.Count);
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

    private static async ValueTask<string?> LoadAccountAddressAsync(
        SqlConnection connection,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(LoadAccountAddressSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    private static async ValueTask<InboxAllocation> AllocateInboxUidAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(AllocateInboxUidSql, connection, transaction);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Local delivery Inbox folder could not be found.");
        }

        return new InboxAllocation(reader.GetInt32(0), reader.GetInt64(1));
    }

    private static async ValueTask<long> InsertDeliveredMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        DeliveryQueuedMessage message,
        int accountId,
        int folderId,
        string messageFileName,
        long uid,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertDeliveredMessageSql, connection, transaction);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderId", SqlDbType.Int).Value = folderId;
        command.Parameters.Add("@MessageFileName", SqlDbType.NVarChar, 255).Value = messageFileName;
        command.Parameters.Add("@MessageFrom", SqlDbType.NVarChar, 255).Value = message.FromAddress;
        command.Parameters.Add("@MessageSize", SqlDbType.BigInt).Value = message.Size;
        command.Parameters.Add("@MessageFlags", SqlDbType.TinyInt).Value = (byte)(message.Flags | ImapMessageFlags.Recent);
        command.Parameters.Add("@MessageCreateTime", SqlDbType.DateTime).Value = message.CreatedUtc.UtcDateTime;
        command.Parameters.Add("@MessageUid", SqlDbType.BigInt).Value = uid;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async ValueTask QueueForIndexingAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(QueueDeliveredMessageForIndexingSql, connection, transaction);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
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

    private sealed record InboxAllocation(int FolderId, long Uid);
}
