using System.Data;
using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerMessageSearchBackfillStore : IMessageSearchBackfillStore
{
    private const string LeaseSql = """
;WITH Candidates AS
(
    SELECT TOP (@BatchSize)
        q.messageid
    FROM hm_message_search_queue AS q WITH (UPDLOCK, READPAST, ROWLOCK)
    INNER JOIN hm_messages AS m
        ON m.messageid = q.messageid
    WHERE
        m.messagetype = 2
        AND q.attempts < @MaxAttempts
        AND (q.nextattemptutc IS NULL OR q.nextattemptutc <= SYSUTCDATETIME())
        AND (q.searchleaseexpiresutc IS NULL OR q.searchleaseexpiresutc <= SYSUTCDATETIME())
    ORDER BY q.queuedutc ASC, q.messageid ASC
)
UPDATE q
SET
    searchleaseowner = @LeaseOwner,
    searchleaseexpiresutc = @LeaseExpiresUtc,
    attempts = attempts + 1,
    lastattemptutc = SYSUTCDATETIME()
OUTPUT
    inserted.messageid,
    m.messageaccountid,
    m.messagefolderid,
    m.messageuid
FROM hm_message_search_queue AS q
INNER JOIN Candidates AS c
    ON c.messageid = q.messageid
INNER JOIN hm_messages AS m
    ON m.messageid = q.messageid;
""";

    private const string SucceededSql = """
DELETE FROM hm_message_search_queue
WHERE messageid = @MessageId
  AND searchleaseowner = @LeaseOwner;
""";

    private const string FailedSql = """
UPDATE hm_message_search_queue
SET
    searchleaseowner = NULL,
    searchleaseexpiresutc = NULL,
    nextattemptutc = @NextAttemptUtc,
    lasterror = @LastError
WHERE messageid = @MessageId
  AND searchleaseowner = @LeaseOwner;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerMessageSearchBackfillStore(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async IAsyncEnumerable<MessageIdentity> LeaseBatchAsync(
        string leaseOwner,
        int batchSize,
        TimeSpan leaseDuration,
        int maxAttempts,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leaseDuration.Ticks);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(LeaseSql, connection);
        command.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;
        command.Parameters.Add("@LeaseOwner", SqlDbType.NVarChar, 128).Value = leaseOwner;
        command.Parameters.Add("@LeaseExpiresUtc", SqlDbType.DateTime2).Value = DateTime.UtcNow.Add(leaseDuration);
        command.Parameters.Add("@MaxAttempts", SqlDbType.Int).Value = maxAttempts;

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

    public async ValueTask MarkSucceededAsync(
        MessageIdentity identity,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(SucceededSql, connection);
        AddLeaseParameters(command, identity, leaseOwner);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask MarkFailedAsync(
        MessageIdentity identity,
        string leaseOwner,
        string error,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ArgumentOutOfRangeException.ThrowIfLessThan(retryDelay.Ticks, 0);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(FailedSql, connection);
        AddLeaseParameters(command, identity, leaseOwner);
        command.Parameters.Add("@NextAttemptUtc", SqlDbType.DateTime2).Value = DateTime.UtcNow.Add(retryDelay);
        command.Parameters.Add("@LastError", SqlDbType.NVarChar, 1024).Value = Truncate(error, 1024);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddLeaseParameters(SqlCommand command, MessageIdentity identity, string leaseOwner)
    {
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = identity.MessageId;
        command.Parameters.Add("@LeaseOwner", SqlDbType.NVarChar, 128).Value = leaseOwner;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
