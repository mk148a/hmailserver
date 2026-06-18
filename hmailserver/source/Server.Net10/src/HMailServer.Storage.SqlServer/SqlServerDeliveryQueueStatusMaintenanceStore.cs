using System.Data;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDeliveryQueueStatusMaintenanceStore
{
    public const string DeleteExpiredStatusesSql = """
;WITH Expired AS
(
    SELECT TOP (@BatchSize)
        statusid
    FROM hm_delivery_queue_status WITH (READPAST, ROWLOCK)
    WHERE eventutc < @CutoffUtc
    ORDER BY eventutc ASC, statusid ASC
)
DELETE statusRows
FROM hm_delivery_queue_status AS statusRows
INNER JOIN Expired AS expired
    ON expired.statusid = statusRows.statusid;

SELECT @@ROWCOUNT;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDeliveryQueueStatusMaintenanceStore(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<int> DeleteExpiredAsync(
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteExpiredStatusesSql, connection);
        command.Parameters.Add("@CutoffUtc", SqlDbType.DateTime2).Value = cutoffUtc;
        command.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
