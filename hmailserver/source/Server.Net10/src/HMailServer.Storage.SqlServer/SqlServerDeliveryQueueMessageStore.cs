using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDeliveryQueueMessageStore : IDeliveryQueueMessageStore
{
    public const string SelectQueuedMessageSql = """
SELECT TOP (1)
    messageid,
    messageaccountid,
    messagefolderid,
    messageuid,
    messagefilename,
    messagefrom,
    messagesize,
    messagecreatetime,
    messageflags,
    messagecurnooftries
FROM hm_messages
WHERE
    messageid = @MessageId
    AND messagetype = 1
    AND messagelocked = 1
    AND messageleaseowner = @LeaseOwner;
""";

    public const string SelectQueuedRecipientsSql = """
SELECT
    recipientid,
    recipientaddress,
    recipientoriginaladdress,
    recipientlocalaccountid
FROM hm_messagerecipients
WHERE recipientmessageid = @MessageId
ORDER BY recipientid ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDeliveryQueueMessageStore(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<DeliveryQueuedMessage?> TryLoadAsync(
        MessageIdentity identity,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(identity.MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var message = await LoadMessageAsync(connection, identity.MessageId, leaseOwner, cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            return null;
        }

        var recipients = await LoadRecipientsAsync(connection, identity.MessageId, cancellationToken).ConfigureAwait(false);
        return message with { Recipients = recipients };
    }

    private static async ValueTask<DeliveryQueuedMessage?> LoadMessageAsync(
        SqlConnection connection,
        long messageId,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectQueuedMessageSql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        command.Parameters.Add("@LeaseOwner", SqlDbType.NVarChar, 128).Value = leaseOwner;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var identity = new MessageIdentity(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt64(3));

        return new DeliveryQueuedMessage(
            identity,
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt64(6),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc)),
            Convert.ToByte(reader.GetValue(8), System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(9), System.Globalization.CultureInfo.InvariantCulture),
            Array.Empty<DeliveryQueueRecipient>());
    }

    private static async ValueTask<IReadOnlyList<DeliveryQueueRecipient>> LoadRecipientsAsync(
        SqlConnection connection,
        long messageId,
        CancellationToken cancellationToken)
    {
        var recipients = new List<DeliveryQueueRecipient>();
        await using var command = new SqlCommand(SelectQueuedRecipientsSql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            recipients.Add(
                new DeliveryQueueRecipient(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3)));
        }

        return recipients;
    }
}
