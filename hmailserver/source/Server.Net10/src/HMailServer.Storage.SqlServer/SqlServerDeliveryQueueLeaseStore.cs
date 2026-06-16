using HMailServer.Core.Abstractions;
using System.Data;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDeliveryQueueLeaseStore : IDeliveryQueueLeaseStore
{
    private const string LeaseSql = """
;WITH Candidates AS
(
    SELECT TOP (@BatchSize)
        messageid,
        messageaccountid,
        messagefolderid,
        messageuid,
        messagelocked,
        messageleaseowner,
        messageleaseexpiresutc
    FROM hm_messages WITH (UPDLOCK, READPAST, ROWLOCK, INDEX(idx_hm_messages_delivery_lease))
    WHERE
        messagetype = 1
        AND messagelocked = 0
        AND messagenexttrytime <= SYSUTCDATETIME()
        AND (messageleaseexpiresutc IS NULL OR messageleaseexpiresutc <= SYSUTCDATETIME())
    ORDER BY messagesize ASC, messagecurnooftries ASC, messageid ASC
)
UPDATE Candidates
SET
    messagelocked = 1,
    messageleaseowner = @LeaseOwner,
    messageleaseexpiresutc = @LeaseExpiresUtc
OUTPUT
    inserted.messageid,
    inserted.messageaccountid,
    inserted.messagefolderid,
    inserted.messageuid;
""";

    private const string CompleteSql = """
BEGIN TRANSACTION;

DELETE FROM hm_messagerecipients
WHERE recipientmessageid = @MessageId
  AND EXISTS
  (
      SELECT 1
      FROM hm_messages
      WHERE messageid = @MessageId
        AND messagetype = 1
        AND messagelocked = 1
        AND messageleaseowner = @LeaseOwner
  );

DELETE FROM hm_messages
WHERE messageid = @MessageId
  AND messagetype = 1
  AND messagelocked = 1
  AND messageleaseowner = @LeaseOwner;

DECLARE @Rows int = @@ROWCOUNT;

COMMIT TRANSACTION;

SELECT @Rows;
""";

    private const string DeferSql = """
UPDATE hm_messages
SET
    messagetype = 1,
    messagelocked = 0,
    messageleaseowner = NULL,
    messageleaseexpiresutc = NULL,
    messagenexttrytime = @NextTryUtc,
    messagecurnooftries = messagecurnooftries + @RetryIncrement
WHERE messageid = @MessageId
  AND messagetype = 1
  AND messagelocked = 1
  AND messageleaseowner = @LeaseOwner;

SELECT @@ROWCOUNT;
""";

    private const string ReleaseSql = """
UPDATE hm_messages
SET
    messagelocked = 0,
    messageleaseowner = NULL,
    messageleaseexpiresutc = NULL
WHERE messageid = @MessageId
  AND messagetype = 1
  AND messagelocked = 1
  AND messageleaseowner = @LeaseOwner;

SELECT @@ROWCOUNT;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDeliveryQueueLeaseStore(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async IAsyncEnumerable<MessageIdentity> LeaseReadyMessagesAsync(
        string leaseOwner,
        int batchSize,
        TimeSpan leaseDuration,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leaseDuration.Ticks);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(LeaseSql, connection);
        command.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;
        command.Parameters.Add("@LeaseOwner", SqlDbType.NVarChar, 128).Value = leaseOwner;
        command.Parameters.Add("@LeaseExpiresUtc", SqlDbType.DateTime2).Value = DateTime.UtcNow.Add(leaseDuration);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new MessageIdentity(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt64(3));
        }
    }

    public async ValueTask<bool> CompleteAsync(
        long messageId,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(CompleteSql, connection);
        AddLeaseIdentityParameters(command, messageId, leaseOwner);
        var affectedRows = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return affectedRows is int rows && rows == 1;
    }

    public async ValueTask<bool> DeferAsync(
        long messageId,
        string leaseOwner,
        TimeSpan retryDelay,
        bool incrementRetryCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        ArgumentOutOfRangeException.ThrowIfLessThan(retryDelay.Ticks, 0);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeferSql, connection);
        AddLeaseIdentityParameters(command, messageId, leaseOwner);
        command.Parameters.Add("@NextTryUtc", SqlDbType.DateTime2).Value = DateTime.UtcNow.Add(retryDelay);
        command.Parameters.Add("@RetryIncrement", SqlDbType.Int).Value = incrementRetryCount ? 1 : 0;
        var affectedRows = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return affectedRows is int rows && rows == 1;
    }

    public async ValueTask<bool> ReleaseAsync(
        long messageId,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(ReleaseSql, connection);
        AddLeaseIdentityParameters(command, messageId, leaseOwner);
        var affectedRows = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return affectedRows is int rows && rows == 1;
    }

    private static void AddLeaseIdentityParameters(SqlCommand command, long messageId, string leaseOwner)
    {
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        command.Parameters.Add("@LeaseOwner", SqlDbType.NVarChar, 128).Value = leaseOwner;
    }
}
