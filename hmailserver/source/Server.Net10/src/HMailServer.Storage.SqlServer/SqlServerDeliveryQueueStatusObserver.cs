using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDeliveryQueueStatusObserver : IDeliveryQueueStatusObserver
{
    public const string InsertStatusSql = """
INSERT INTO hm_delivery_queue_status
(
    messageid,
    eventutc,
    eventkind,
    leaseowner,
    targetkey,
    targetdomainname,
    targetkind,
    recipientcount,
    retrycount,
    retrydelaymilliseconds,
    failurekind,
    description
)
VALUES
(
    @MessageId,
    SYSUTCDATETIME(),
    @EventKind,
    @LeaseOwner,
    @TargetKey,
    @TargetDomainName,
    @TargetKind,
    @RecipientCount,
    @RetryCount,
    @RetryDelayMilliseconds,
    @FailureKind,
    @Description
);
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDeliveryQueueStatusObserver(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask RecordAsync(
        DeliveryQueueStatusEvent statusEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statusEvent);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertStatusSql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = statusEvent.MessageId;
        command.Parameters.Add("@EventKind", SqlDbType.NVarChar, 64).Value = statusEvent.Kind.ToString();
        command.Parameters.Add("@LeaseOwner", SqlDbType.NVarChar, 128).Value = Truncate(statusEvent.LeaseOwner, 128);
        command.Parameters.Add("@TargetKey", SqlDbType.NVarChar, 255).Value = ToDbString(statusEvent.TargetKey, 255);
        command.Parameters.Add("@TargetDomainName", SqlDbType.NVarChar, 255).Value = ToDbString(statusEvent.TargetDomainName, 255);
        command.Parameters.Add("@TargetKind", SqlDbType.NVarChar, 64).Value = ToDbString(statusEvent.TargetKind?.ToString(), 64);
        command.Parameters.Add("@RecipientCount", SqlDbType.Int).Value = statusEvent.RecipientCount;
        command.Parameters.Add("@RetryCount", SqlDbType.Int).Value = statusEvent.RetryCount;
        command.Parameters.Add("@RetryDelayMilliseconds", SqlDbType.BigInt).Value = ToDbRetryDelay(statusEvent.RetryDelay);
        command.Parameters.Add("@FailureKind", SqlDbType.NVarChar, 64).Value = ToDbString(statusEvent.FailureKind?.ToString(), 64);
        command.Parameters.Add("@Description", SqlDbType.NVarChar, 1024).Value = ToDbString(statusEvent.Description, 1024);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static object ToDbRetryDelay(TimeSpan? retryDelay) =>
        retryDelay is null
            ? DBNull.Value
            : retryDelay.Value.Ticks / TimeSpan.TicksPerMillisecond;

    private static object ToDbString(string? value, int maxLength) =>
        string.IsNullOrEmpty(value)
            ? DBNull.Value
            : Truncate(value, maxLength);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..maxLength];
}
