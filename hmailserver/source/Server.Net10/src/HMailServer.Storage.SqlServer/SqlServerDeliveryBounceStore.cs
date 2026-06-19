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

        var messageData = BuildBounceMessage(
            _options,
            originalMessage,
            failedRecipients,
            failureDescription,
            DateTimeOffset.UtcNow);
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

    public static byte[] BuildBounceMessage(
        DeliveryBounceOptions options,
        DeliveryQueuedMessage originalMessage,
        IReadOnlyList<DeliveryQueueRecipient> failedRecipients,
        string failureDescription,
        DateTimeOffset generatedUtc)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(originalMessage);
        ArgumentNullException.ThrowIfNull(failedRecipients);

        var recipientsText = FormatRecipients(failedRecipients);
        var truncatedFailureDescription = Truncate(
            NormalizeBodyLineEndings(failureDescription),
            Math.Max(0, options.MaxFailureDescriptionLength));
        var subject = RenderTemplate(
            options.SubjectTemplate,
            options,
            originalMessage,
            failedRecipients,
            recipientsText,
            truncatedFailureDescription,
            generatedUtc);
        var body = RenderTemplate(
            options.BodyTemplate,
            options,
            originalMessage,
            failedRecipients,
            recipientsText,
            truncatedFailureDescription,
            generatedUtc);

        var builder = new StringBuilder();
        builder.Append("From: ").Append(SanitizeHeaderValue(options.MailerDaemonAddress)).Append("\r\n");
        builder.Append("To: ").Append(SanitizeHeaderValue(originalMessage.FromAddress)).Append("\r\n");
        builder.Append("Subject: ").Append(SanitizeHeaderValue(subject)).Append("\r\n");
        builder.Append("Auto-Submitted: auto-replied\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        builder.Append("X-hMailServer-Queue-Message-Id: ").Append(originalMessage.Identity.MessageId).Append("\r\n");
        builder.Append("X-hMailServer-Delivery-Attempt: ").Append(originalMessage.CurrentRetryCount).Append("\r\n");
        builder.Append("X-hMailServer-Failed-Recipients: ")
            .Append(SanitizeHeaderValue(Truncate(string.Join(", ", failedRecipients.Select(static recipient => recipient.Address)), 900)))
            .Append("\r\n");
        builder.Append("Date: ").Append(generatedUtc.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
        builder.Append("\r\n");
        builder.Append(NormalizeBodyLineEndings(body));
        if (builder.Length < 2 || builder[^2] != '\r' || builder[^1] != '\n')
        {
            builder.Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string RenderTemplate(
        string template,
        DeliveryBounceOptions options,
        DeliveryQueuedMessage originalMessage,
        IReadOnlyList<DeliveryQueueRecipient> failedRecipients,
        string recipientsText,
        string failureDescription,
        DateTimeOffset generatedUtc)
    {
        var rendered = string.IsNullOrEmpty(template)
            ? DeliveryBounceOptions.DefaultBodyTemplate
            : template;
        return rendered
            .Replace("{ServerName}", options.ServerName, StringComparison.Ordinal)
            .Replace("{MessageId}", originalMessage.Identity.MessageId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{MessageUid}", originalMessage.Identity.Uid.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{AccountId}", originalMessage.Identity.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{FolderId}", originalMessage.Identity.FolderId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{Sender}", originalMessage.FromAddress, StringComparison.Ordinal)
            .Replace("{FileName}", originalMessage.FileName, StringComparison.Ordinal)
            .Replace("{Size}", originalMessage.Size.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{MessageState}", originalMessage.Flags.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{CreatedUtc}", originalMessage.CreatedUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{RetryCount}", originalMessage.CurrentRetryCount.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{DeliveryAttempt}", (originalMessage.CurrentRetryCount + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{FailedRecipientCount}", failedRecipients.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{FailedRecipientAddresses}", FormatRecipientAddresses(failedRecipients), StringComparison.Ordinal)
            .Replace("{FirstFailedRecipient}", failedRecipients.Count == 0 ? string.Empty : failedRecipients[0].Address, StringComparison.Ordinal)
            .Replace("{RuleForcedRouteId}", originalMessage.RuleForcedRouteId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{RuleBindAddress}", originalMessage.RuleBindAddress ?? string.Empty, StringComparison.Ordinal)
            .Replace("{Recipients}", recipientsText, StringComparison.Ordinal)
            .Replace("{FailureDescription}", failureDescription, StringComparison.Ordinal)
            .Replace("{GeneratedUtc}", generatedUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static string FormatRecipients(IReadOnlyList<DeliveryQueueRecipient> failedRecipients)
    {
        if (failedRecipients.Count == 0)
        {
            return " - (no failed recipients were provided)";
        }

        var builder = new StringBuilder();
        foreach (var recipient in failedRecipients)
        {
            builder.Append(" - ").Append(recipient.Address);
            if (!recipient.OriginalAddress.Equals(recipient.Address, StringComparison.OrdinalIgnoreCase))
            {
                builder.Append(" (original: ").Append(recipient.OriginalAddress).Append(')');
            }

            builder.Append("\r\n");
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string FormatRecipientAddresses(IReadOnlyList<DeliveryQueueRecipient> failedRecipients) =>
        string.Join(", ", failedRecipients.Select(static recipient => recipient.Address));

    private static string NormalizeBodyLineEndings(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);
    }

    private static string SanitizeHeaderValue(string value) =>
        NormalizeBodyLineEndings(value)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Trim();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..maxLength];

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
