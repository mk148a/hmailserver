using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;
using MimeKit;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapMessageAppendStore : IImapMessageAppendStore
{
    public const string AllocateUidSql = """
UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)
SET foldercurrentuid = foldercurrentuid + 1
OUTPUT INSERTED.foldercurrentuid, INSERTED.foldercreationtime
WHERE
    folderaccountid = @DestinationAccountId
    AND folderid = @DestinationFolderId;
""";

    public const string InsertAppendedMessageSql = """
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

    public const string QueueAppendedMessageForIndexingSql = """
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

    private const string LoadAccountAddressSql = """
SELECT TOP (1) accountaddress
FROM hm_accounts
WHERE accountid = @AccountId;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver _pathResolver;

    public SqlServerImapMessageAppendStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
    }

    public async ValueTask<ImapAppendResult> AppendAsync(
        ImapAppendRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.DestinationAccountId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DestinationFolderId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var accountAddress = await LoadDestinationAccountAddressAsync(
            connection,
            request.DestinationAccountId,
            cancellationToken).ConfigureAwait(false);
        if (request.DestinationAccountId > 0 && string.IsNullOrWhiteSpace(accountAddress))
        {
            throw new InvalidOperationException("Destination account could not be found.");
        }

        var messageFileName = Guid.NewGuid().ToString("N") + ".eml";
        var messagePath = _pathResolver.Resolve(
            messageFileName,
            request.DestinationAccountId,
            request.DestinationFolderId,
            accountAddress);
        if (messagePath is null)
        {
            throw new IOException("Destination message path is invalid.");
        }

        var directory = Path.GetDirectoryName(messagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(messagePath, request.RawMessage, cancellationToken).ConfigureAwait(false);

        try
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var allocation = await AllocateDestinationUidAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
                var messageId = await InsertMessageAsync(
                    connection,
                    transaction,
                    request,
                    messageFileName,
                    allocation.Uid,
                    cancellationToken).ConfigureAwait(false);
                await QueueForIndexingAsync(connection, transaction, messageId, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                return new ImapAppendResult(
                    new MessageIdentity(messageId, request.DestinationAccountId, request.DestinationFolderId, allocation.Uid),
                    GetUidValidity(allocation.FolderCreationTime));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            TryDelete(messagePath);
            throw;
        }
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

    private static async ValueTask<UidAllocation> AllocateDestinationUidAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ImapAppendRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(AllocateUidSql, connection, transaction);
        command.Parameters.Add("@DestinationAccountId", SqlDbType.Int).Value = request.DestinationAccountId;
        command.Parameters.Add("@DestinationFolderId", SqlDbType.Int).Value = request.DestinationFolderId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Failed to allocate destination message UID.");
        }

        return new UidAllocation(reader.GetInt64(0), reader.GetDateTime(1));
    }

    private static async ValueTask<long> InsertMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ImapAppendRequest request,
        string messageFileName,
        long uid,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertAppendedMessageSql, connection, transaction);
        command.Parameters.Add("@DestinationAccountId", SqlDbType.Int).Value = request.DestinationAccountId;
        command.Parameters.Add("@DestinationFolderId", SqlDbType.Int).Value = request.DestinationFolderId;
        command.Parameters.Add("@MessageFileName", SqlDbType.NVarChar, 255).Value = messageFileName;
        command.Parameters.Add("@MessageFrom", SqlDbType.NVarChar, 255).Value = ExtractFromAddress(request.RawMessage);
        command.Parameters.Add("@MessageSize", SqlDbType.BigInt).Value = request.RawMessage.LongLength;
        command.Parameters.Add("@MessageFlags", SqlDbType.TinyInt).Value = (byte)(request.Flags | ImapMessageFlags.Recent);
        command.Parameters.Add("@MessageCreateTime", SqlDbType.DateTime).Value =
            (request.InternalDateUtc ?? DateTimeOffset.UtcNow).UtcDateTime;
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
        await using var command = new SqlCommand(QueueAppendedMessageForIndexingSql, connection, transaction);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ExtractFromAddress(byte[] rawMessage)
    {
        try
        {
            using var stream = new MemoryStream(rawMessage);
            var message = MimeMessage.Load(stream);
            return message.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static long GetUidValidity(DateTime creationTime)
    {
        var utc = DateTime.SpecifyKind(creationTime, DateTimeKind.Utc);
        return Math.Max(1, new DateTimeOffset(utc).ToUnixTimeSeconds());
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

    private sealed record UidAllocation(long Uid, DateTime FolderCreationTime);
}
