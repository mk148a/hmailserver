using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDeliveryQueueStatusMetricsStore : IDeliveryQueueStatusMetricsStore
{
    public const string SelectCountsByKindSql = """
SELECT
    eventkind,
    COUNT_BIG(*) AS eventcount
FROM hm_delivery_queue_status WITH (READCOMMITTEDLOCK)
WHERE eventutc >= @SinceUtc
  AND eventutc < @UntilUtc
GROUP BY eventkind
ORDER BY eventkind ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDeliveryQueueStatusMetricsStore(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<DeliveryQueueStatusMetricsSnapshot> GetSnapshotAsync(
        DateTimeOffset sinceUtc,
        DateTimeOffset untilUtc,
        CancellationToken cancellationToken)
    {
        if (untilUtc < sinceUtc)
        {
            throw new ArgumentException("The metrics window end must be greater than or equal to the start.", nameof(untilUtc));
        }

        var counts = new List<DeliveryQueueStatusKindMetric>();
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(SelectCountsByKindSql, connection);
        command.Parameters.Add("@SinceUtc", SqlDbType.DateTime2).Value = sinceUtc.UtcDateTime;
        command.Parameters.Add("@UntilUtc", SqlDbType.DateTime2).Value = untilUtc.UtcDateTime;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            counts.Add(
                new DeliveryQueueStatusKindMetric(
                    reader.GetString(0),
                    reader.GetInt64(1)));
        }

        return new DeliveryQueueStatusMetricsSnapshot(
            sinceUtc.ToUniversalTime(),
            untilUtc.ToUniversalTime(),
            counts);
    }
}
