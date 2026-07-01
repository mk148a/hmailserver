using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDeliveryQueueAdministrationStore : IDeliveryQueueAdministrationStore
{
    public const string ResetDeliveryTimeSql = """
UPDATE hm_messages
SET
    messagenexttrytime = DATEADD(MINUTE, -1, SYSUTCDATETIME()),
    messagetype = 1
WHERE messageid = @MessageId
  AND messagetype IN (1, 3);

SELECT @@ROWCOUNT;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDeliveryQueueAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<bool> ResetDeliveryTimeAsync(
        long messageId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(ResetDeliveryTimeSql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.BigInt).Value = messageId;
        var affectedRows = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return affectedRows is int rows && rows == 1;
    }
}
