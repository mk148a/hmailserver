using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImportMessageFromFileStore : IImportMessageFromFileStore
{
    public const string FindMessageSql = """
SELECT TOP (1) messageid
FROM hm_messages
WHERE messagefilename = @FileName;
""";

    public const string UpdateMessageFileNameSql = """
UPDATE hm_messages
SET messagefilename = @FileName
WHERE messageid = @MessageId;
""";

    public const string FindFolderSql = """
SELECT TOP (1) folderid
FROM hm_imapfolders
WHERE
    folderaccountid = @AccountId
    AND folderparentid = @ParentFolderId
    AND LOWER(foldername) = LOWER(@FolderName);
""";

    public const string AllocateFolderUidSql = """
UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)
SET foldercurrentuid = foldercurrentuid + 1
OUTPUT INSERTED.foldercurrentuid
WHERE
    folderaccountid = @AccountId
    AND folderid = @FolderId;
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
    @FileName,
    2,
    @MessageFrom,
    @MessageSize,
    0,
    CONVERT(datetime, '1901-01-01', 120),
    32,
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

    public const string InsertQueuedMessageSql = """
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
    0,
    0,
    @FileName,
    1,
    @MessageFrom,
    @MessageSize,
    0,
    CONVERT(datetime, '1901-01-01', 120),
    32,
    @MessageCreateTime,
    1,
    0
);
""";

    public const string InsertRecipientSql = """
INSERT INTO hm_messagerecipients
(
    recipientmessageid,
    recipientaddress,
    recipientlocalaccountid,
    recipientoriginaladdress
)
VALUES
(
    @MessageId,
    @RecipientAddress,
    @LocalAccountId,
    @OriginalAddress
);
""";

    public const string UnlockQueuedMessageSql = """
UPDATE hm_messages
SET messagelocked = 0
WHERE
    messageid = @MessageId
    AND messagetype = 1;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerImportMessageFromFileStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<ImportedMessageReference?> FindExistingMessageAsync(
        string? partialFileName,
        string fullFileName,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(partialFileName))
        {
            var partialId = await FindMessageIdAsync(
                connection,
                partialFileName,
                cancellationToken).ConfigureAwait(false);
            if (partialId.HasValue)
            {
                return new ImportedMessageReference(partialId.Value, IsPartialFileName: true);
            }
        }

        var fullId = await FindMessageIdAsync(
            connection,
            fullFileName,
            cancellationToken).ConfigureAwait(false);
        return fullId.HasValue
            ? new ImportedMessageReference(fullId.Value, IsPartialFileName: false)
            : null;
    }

    public async ValueTask<bool> UpdateMessageFileNameAsync(
        long messageId,
        string partialFileName,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateMessageFileNameSql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = partialFileName;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<long?> FindAccountFolderAsync(
        int accountId,
        IReadOnlyList<string> folderPath,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentNullException.ThrowIfNull(folderPath);
        if (folderPath.Count == 0)
        {
            return null;
        }

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        long? folderId = null;
        var parentFolderId = -1L;
        foreach (var segment in folderPath)
        {
            folderId = await FindFolderIdAsync(
                connection,
                accountId,
                parentFolderId,
                segment,
                cancellationToken).ConfigureAwait(false);
            if (!folderId.HasValue)
            {
                return null;
            }

            parentFolderId = folderId.Value;
        }

        return folderId;
    }

    public async ValueTask ImportDeliveredMessageAsync(
        ImportedDeliveredMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(message.AccountId);

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var uid = await AllocateFolderUidAsync(
                connection,
                transaction,
                message.AccountId,
                message.FolderId,
                cancellationToken).ConfigureAwait(false);
            var messageId = await InsertDeliveredMessageAsync(
                connection,
                transaction,
                message,
                uid,
                cancellationToken).ConfigureAwait(false);
            await QueueForIndexingAsync(
                connection,
                transaction,
                messageId,
                cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask ImportQueuedMessageAsync(
        ImportedQueuedMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Recipients.Count == 0)
        {
            throw new InvalidOperationException("Imported queue message must contain a local recipient.");
        }

        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var messageId = await InsertQueuedMessageAsync(
                connection,
                transaction,
                message,
                cancellationToken).ConfigureAwait(false);
            foreach (var recipient in message.Recipients)
            {
                await InsertRecipientAsync(
                    connection,
                    transaction,
                    messageId,
                    recipient,
                    cancellationToken).ConfigureAwait(false);
            }

            await UnlockQueuedMessageAsync(
                connection,
                transaction,
                messageId,
                cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask<long?> FindMessageIdAsync(
        SqlConnection connection,
        string fileName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(FindMessageSql, connection);
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = fileName;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull
            ? null
            : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async ValueTask<long?> FindFolderIdAsync(
        SqlConnection connection,
        int accountId,
        long parentFolderId,
        string folderName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(FindFolderSql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@ParentFolderId", SqlDbType.BigInt).Value = parentFolderId;
        command.Parameters.Add("@FolderName", SqlDbType.NVarChar, 255).Value = folderName;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull
            ? null
            : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async ValueTask<long> AllocateFolderUidAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int accountId,
        long folderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(AllocateFolderUidSql, connection, transaction);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@FolderId", SqlDbType.BigInt).Value = folderId;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null or DBNull)
        {
            throw new InvalidOperationException("The destination IMAP folder could not be found.");
        }

        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async ValueTask<long> InsertDeliveredMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ImportedDeliveredMessage message,
        long uid,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertDeliveredMessageSql, connection, transaction);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = message.AccountId;
        command.Parameters.Add("@FolderId", SqlDbType.BigInt).Value = message.FolderId;
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = message.FileName;
        command.Parameters.Add("@MessageFrom", SqlDbType.NVarChar, 255).Value = message.FromAddress;
        command.Parameters.Add("@MessageSize", SqlDbType.BigInt).Value = message.Size;
        command.Parameters.Add("@MessageCreateTime", SqlDbType.DateTime).Value = message.CreatedUtc.UtcDateTime;
        command.Parameters.Add("@MessageUid", SqlDbType.BigInt).Value = uid;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async ValueTask QueueForIndexingAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            QueueDeliveredMessageForIndexingSql,
            connection,
            transaction);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<long> InsertQueuedMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ImportedQueuedMessage message,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertQueuedMessageSql, connection, transaction);
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = message.FileName;
        command.Parameters.Add("@MessageFrom", SqlDbType.NVarChar, 255).Value = message.FromAddress;
        command.Parameters.Add("@MessageSize", SqlDbType.BigInt).Value = message.Size;
        command.Parameters.Add("@MessageCreateTime", SqlDbType.DateTime).Value = message.CreatedUtc.UtcDateTime;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async ValueTask InsertRecipientAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long messageId,
        SmtpResolvedRecipient recipient,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertRecipientSql, connection, transaction);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        command.Parameters.Add("@RecipientAddress", SqlDbType.NVarChar, 255).Value = recipient.Address;
        command.Parameters.Add("@LocalAccountId", SqlDbType.Int).Value = recipient.LocalAccountId;
        command.Parameters.Add("@OriginalAddress", SqlDbType.NVarChar, 255).Value = recipient.OriginalAddress;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask UnlockQueuedMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(UnlockQueuedMessageSql, connection, transaction);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
        {
            throw new InvalidOperationException("The imported queue message could not be unlocked.");
        }
    }
}
