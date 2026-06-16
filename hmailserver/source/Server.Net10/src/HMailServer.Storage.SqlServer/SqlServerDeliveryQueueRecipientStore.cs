using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDeliveryQueueRecipientStore : IDeliveryQueueRecipientStore
{
    public const string DeleteRecipientsSqlTemplate = """
DELETE FROM hm_messagerecipients
WHERE recipientmessageid = @MessageId
  AND recipientid IN ({0})
  AND EXISTS
  (
      SELECT 1
      FROM hm_messages
      WHERE messageid = @MessageId
        AND messagetype = 1
        AND messagelocked = 1
        AND messageleaseowner = @LeaseOwner
  );

SELECT @@ROWCOUNT;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDeliveryQueueRecipientStore(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<bool> DeleteRecipientsAsync(
        long messageId,
        string leaseOwner,
        IReadOnlyList<long> recipientIds,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        if (recipientIds.Count == 0)
        {
            return true;
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var parameterNames = recipientIds
            .Select(static (_, index) => "@RecipientId" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        await using var command = new SqlCommand(
            string.Format(System.Globalization.CultureInfo.InvariantCulture, DeleteRecipientsSqlTemplate, string.Join(", ", parameterNames)),
            connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        command.Parameters.Add("@LeaseOwner", SqlDbType.NVarChar, 128).Value = leaseOwner;
        for (var i = 0; i < recipientIds.Count; i++)
        {
            command.Parameters.Add(parameterNames[i], SqlDbType.BigInt).Value = recipientIds[i];
        }

        var deleted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(deleted, System.Globalization.CultureInfo.InvariantCulture) == recipientIds.Count;
    }
}
