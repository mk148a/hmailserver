using System.Data;
using System.Globalization;
using System.Text;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerServerStatusAdministrationStore : IServerStatusAdministrationStore
{
    public const string SelectUndeliveredMessagesSql = """
SELECT
    messageid,
    messagecurnooftries,
    messagecreatetime,
    messagefrom,
    messagenexttrytime,
    messagefilename,
    messagelocked
FROM hm_messages
WHERE messagetype = 1 OR messagetype = 3
ORDER BY messageid ASC;
""";

    public const string SelectRecipientsSql = """
SELECT recipientaddress
FROM hm_messagerecipients
WHERE recipientmessageid = @MessageId;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly ServerStatusRuntimeState _runtimeState;
    private readonly string _dataDirectory;

    public SqlServerServerStatusAdministrationStore(
        SqlServerConnectionFactory connectionFactory,
        MessageFileSearchDocumentSourceOptions options,
        ServerStatusRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(runtimeState);

        _connectionFactory = connectionFactory;
        _runtimeState = runtimeState;
        _dataDirectory = options.NormalizedDataDirectory;
    }

    public async ValueTask<ServerStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken)
    {
        var runtime = _runtimeState.Capture();
        var undeliveredMessages = await LoadUndeliveredMessagesAsync(cancellationToken).ConfigureAwait(false);

        return new ServerStatusSnapshot(
            undeliveredMessages,
            runtime.StartTime,
            runtime.ProcessedMessages,
            runtime.RemovedViruses,
            runtime.RemovedSpamMessages,
            runtime.SessionCounts,
            runtime.ThreadID);
    }

    private async ValueTask<string> LoadUndeliveredMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
            var messages = await LoadQueuedMessagesAsync(connection, cancellationToken).ConfigureAwait(false);

            var builder = new StringBuilder();
            foreach (var message in messages)
            {
                var recipients = await LoadRecipientsAsync(connection, message.MessageId, cancellationToken).ConfigureAwait(false);
                var fileName = ResolveLegacyMessageFileName(message.FileName);

                if (builder.Length > 0)
                {
                    builder.Append("\r\n");
                }

                builder
                    .Append(message.MessageId.ToString(CultureInfo.InvariantCulture))
                    .Append('\t')
                    .Append(message.CreateTime)
                    .Append('\t')
                    .Append(message.From)
                    .Append('\t')
                    .Append(recipients)
                    .Append('\t')
                    .Append(message.NextTryTime)
                    .Append('\t')
                    .Append(fileName)
                    .Append('\t')
                    .Append(message.Locked)
                    .Append('\t')
                    .Append(message.CurrentNumberOfTries);
            }

            return builder.ToString();
        }
        catch (SqlException)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
        catch (TimeoutException)
        {
            return string.Empty;
        }
    }

    private static async ValueTask<IReadOnlyList<QueuedStatusMessage>> LoadQueuedMessagesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var messages = new List<QueuedStatusMessage>();
        await using var command = new SqlCommand(SelectUndeliveredMessagesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messages.Add(
                new QueuedStatusMessage(
                    reader.GetInt64(0),
                    Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                    FormatLegacyDateTime(reader.GetValue(2)),
                    reader.GetString(3),
                    FormatLegacyDateTime(reader.GetValue(4)),
                    reader.GetString(5),
                    Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture)));
        }

        return messages;
    }

    private static async ValueTask<string> LoadRecipientsAsync(
        SqlConnection connection,
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectRecipientsSql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;

        var recipients = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            recipients.Add(reader.GetString(0));
        }

        return string.Join(",", recipients);
    }

    private string ResolveLegacyMessageFileName(string messageFileName) =>
        Path.IsPathRooted(messageFileName)
            ? messageFileName
            : Path.Combine(_dataDirectory, messageFileName);

    private static string FormatLegacyDateTime(object value) =>
        value switch
        {
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DBNull => string.Empty,
            null => string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

    private sealed record QueuedStatusMessage(
        long MessageId,
        int CurrentNumberOfTries,
        string CreateTime,
        string From,
        string NextTryTime,
        string FileName,
        int Locked);
}
