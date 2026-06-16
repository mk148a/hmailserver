using System.Data;
using System.Net.Mail;
using System.Text;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDeliveryBounceStore : IDeliveryBounceStore
{
    public const string InsertBounceMessageSql = SqlServerSmtpMessageReceiver.InsertQueuedMessageSql;

    public const string InsertBounceRecipientSql = SqlServerSmtpMessageReceiver.InsertRecipientSql;

    public const string UnlockBounceMessageSql = SqlServerSmtpMessageReceiver.UnlockQueuedMessageSql;

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver _pathResolver;
    private readonly DeliveryBounceOptions _options;

    public SqlServerDeliveryBounceStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver,
        DeliveryBounceOptions options)
    {
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
        _options = options;
    }

    public async ValueTask<DeliveryBounceResult> SubmitBounceAsync(
        DeliveryQueuedMessage originalMessage,
        IReadOnlyList<DeliveryQueueRecipient> failedRecipients,
        string failureDescription,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalMessage);

        if (string.IsNullOrWhiteSpace(originalMessage.FromAddress))
        {
            return DeliveryBounceResult.Skipped("Original sender is empty.");
        }

        if (IsMailerDaemon(originalMessage.FromAddress))
        {
            return DeliveryBounceResult.Skipped("Original sender is already a mailer daemon address.");
        }

        if (!MailAddress.TryCreate(originalMessage.FromAddress, out _))
        {
            return DeliveryBounceResult.Skipped("Original sender address is invalid.");
        }

        var messageData = BuildBounceMessage(originalMessage, failedRecipients, failureDescription);
        var messageFileName = Guid.NewGuid().ToString("N") + ".eml";
        var messagePath = _pathResolver.Resolve(messageFileName, accountId: 0, folderId: 0, accountAddress: null);
        if (messagePath is null)
        {
            throw new IOException("Bounce message path is invalid.");
        }

        await File.WriteAllBytesAsync(messagePath, messageData, cancellationToken).ConfigureAwait(false);

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var messageId = await InsertBounceMessageAsync(
                    connection,
                    transaction,
                    originalMessage,
                    messageFileName,
                    messageData.Length,
                    cancellationToken).ConfigureAwait(false);
                await InsertBounceRecipientAsync(
                    connection,
                    transaction,
                    messageId,
                    originalMessage.FromAddress,
                    cancellationToken).ConfigureAwait(false);
                await UnlockBounceMessageAsync(connection, transaction, messageId, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return DeliveryBounceResult.SubmittedResult();
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

    private async ValueTask<long> InsertBounceMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        DeliveryQueuedMessage originalMessage,
        string messageFileName,
        int messageSize,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertBounceMessageSql, connection, transaction);
        command.Parameters.Add("@MessageFileName", SqlDbType.NVarChar, 255).Value = messageFileName;
        command.Parameters.Add("@MessageFrom", SqlDbType.NVarChar, 255).Value = _options.MailerDaemonAddress;
        command.Parameters.Add("@MessageSize", SqlDbType.BigInt).Value = messageSize;
        command.Parameters.Add("@MessageFlags", SqlDbType.TinyInt).Value = ImapMessageFlags.Recent;
        command.Parameters.Add("@MessageCreateTime", SqlDbType.DateTime).Value = DateTime.UtcNow;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async ValueTask InsertBounceRecipientAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long messageId,
        string recipientAddress,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertBounceRecipientSql, connection, transaction);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        command.Parameters.Add("@RecipientAddress", SqlDbType.NVarChar, 255).Value = recipientAddress;
        command.Parameters.Add("@LocalAccountId", SqlDbType.Int).Value = 0;
        command.Parameters.Add("@OriginalAddress", SqlDbType.NVarChar, 255).Value = recipientAddress;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask UnlockBounceMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(UnlockBounceMessageSql, connection, transaction);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private byte[] BuildBounceMessage(
        DeliveryQueuedMessage originalMessage,
        IReadOnlyList<DeliveryQueueRecipient> failedRecipients,
        string failureDescription)
    {
        var builder = new StringBuilder();
        builder.Append("From: ").Append(_options.MailerDaemonAddress).Append("\r\n");
        builder.Append("To: ").Append(originalMessage.FromAddress).Append("\r\n");
        builder.Append("Subject: ").Append(_options.Subject).Append("\r\n");
        builder.Append("Auto-Submitted: auto-replied\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        builder.Append("Date: ").Append(DateTimeOffset.UtcNow.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
        builder.Append("\r\n");
        builder.Append("Your message could not be delivered.\r\n\r\n");
        builder.Append("Original queue message id: ").Append(originalMessage.Identity.MessageId).Append("\r\n");
        builder.Append("Recipients:\r\n");
        foreach (var recipient in failedRecipients)
        {
            builder.Append(" - ").Append(recipient.Address).Append("\r\n");
        }

        builder.Append("\r\nReason:\r\n");
        builder.Append(failureDescription).Append("\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static bool IsMailerDaemon(string address)
    {
        var at = address.IndexOf('@', StringComparison.Ordinal);
        var localPart = at > 0 ? address[..at] : address;
        return localPart.Equals("MAILER-DAEMON", StringComparison.OrdinalIgnoreCase) ||
               localPart.Equals("postmaster", StringComparison.OrdinalIgnoreCase);
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
