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
            var allocation = await AllocateInboxUidAsync(
                connection,
                transaction,
                message.AccountId,
                cancellationToken).ConfigureAwait(false);
            var messageId = await InsertDeliveredMessageAsync(
                connection,
                transaction,
                message,
                allocation,
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

    private static async ValueTask<InboxAllocation> AllocateInboxUidAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(AllocateInboxUidSql, connection, transaction);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken)
            .ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The destination account Inbox could not be found.");
        }

        return new InboxAllocation(reader.GetInt64(0), reader.GetInt64(1));
    }

    private static async ValueTask<long> InsertDeliveredMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ImportedDeliveredMessage message,
        InboxAllocation allocation,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertDeliveredMessageSql, connection, transaction);
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = message.AccountId;
        command.Parameters.Add("@FolderId", SqlDbType.BigInt).Value = allocation.FolderId;
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 255).Value = message.FileName;
        command.Parameters.Add("@MessageFrom", SqlDbType.NVarChar, 255).Value = message.FromAddress;
        command.Parameters.Add("@MessageSize", SqlDbType.BigInt).Value = message.Size;
        command.Parameters.Add("@MessageCreateTime", SqlDbType.DateTime).Value = message.CreatedUtc.UtcDateTime;
        command.Parameters.Add("@MessageUid", SqlDbType.BigInt).Value = allocation.Uid;
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

    private sealed record InboxAllocation(long FolderId, long Uid);
}
