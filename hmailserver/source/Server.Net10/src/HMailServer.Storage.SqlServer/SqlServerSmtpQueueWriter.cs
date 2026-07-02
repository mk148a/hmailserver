using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSmtpQueueWriter : ISmtpQueueWriter
{
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
    messageuid,
    messageruleforcedrouteid,
    messagerulebindaddress
)
OUTPUT INSERTED.messageid
VALUES
(
    0,
    0,
    @MessageFileName,
    1,
    @MessageFrom,
    @MessageSize,
    0,
    CONVERT(datetime, '1901-01-01', 120),
    @MessageFlags,
    @MessageCreateTime,
    1,
    0,
    @RuleForcedRouteId,
    @RuleBindAddress
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
    private readonly MessageFilePathResolver _pathResolver;

    public SqlServerSmtpQueueWriter(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
    }

    public async ValueTask EnqueueAsync(
        SmtpQueueWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Recipients.Count == 0)
        {
            throw new InvalidOperationException("Queued SMTP message must contain at least one recipient.");
        }

        var messageFileName = Guid.NewGuid().ToString("N") + ".eml";
        var messagePath = _pathResolver.Resolve(
            messageFileName,
            accountId: 0,
            folderId: 0,
            accountAddress: null);
        if (messagePath is null)
        {
            throw new IOException("Queued SMTP message path is invalid.");
        }

        var directory = Path.GetDirectoryName(messagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(messagePath, request.MessageData, cancellationToken).ConfigureAwait(false);

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var messageId = await InsertQueuedMessageAsync(
                    connection,
                    transaction,
                    request,
                    messageFileName,
                    cancellationToken).ConfigureAwait(false);
                foreach (var recipient in request.Recipients)
                {
                    await InsertRecipientAsync(
                        connection,
                        transaction,
                        messageId,
                        recipient,
                        cancellationToken).ConfigureAwait(false);
                }

                await UnlockQueuedMessageAsync(connection, transaction, messageId, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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

    private static async ValueTask<long> InsertQueuedMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SmtpQueueWriteRequest request,
        string messageFileName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertQueuedMessageSql, connection, transaction);
        command.Parameters.Add("@MessageFileName", SqlDbType.NVarChar, 255).Value = messageFileName;
        command.Parameters.Add("@MessageFrom", SqlDbType.NVarChar, 255).Value = request.MailFrom;
        command.Parameters.Add("@MessageSize", SqlDbType.BigInt).Value = request.MessageData.LongLength;
        command.Parameters.Add("@MessageFlags", SqlDbType.TinyInt).Value = request.MessageFlags;
        command.Parameters.Add("@MessageCreateTime", SqlDbType.DateTime).Value = request.ReceivedUtc.UtcDateTime;
        command.Parameters.Add("@RuleForcedRouteId", SqlDbType.Int).Value = request.RuleForcedRouteId > 0
            ? request.RuleForcedRouteId
            : (object)DBNull.Value;
        command.Parameters.Add("@RuleBindAddress", SqlDbType.NVarChar, 64).Value = string.IsNullOrWhiteSpace(request.RuleBindAddress)
            ? (object)DBNull.Value
            : request.RuleBindAddress;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
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
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
