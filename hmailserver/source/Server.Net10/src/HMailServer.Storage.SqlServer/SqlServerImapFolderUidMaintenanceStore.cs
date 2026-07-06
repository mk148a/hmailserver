using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerImapFolderUidMaintenanceStore : IImapFolderUidMaintenanceStore
{
    public const string ReadFolderMaximumUidsSql = """
SELECT messagefolderid, MAX(messageuid) AS messageuid
FROM hm_messages
GROUP BY messagefolderid;
""";

    public const string AdvanceFolderUidSql = """
UPDATE hm_imapfolders
SET foldercurrentuid = @MessageUid
WHERE folderid = @MessageFolderId
  AND foldercurrentuid < @MessageUid;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerImapFolderUidMaintenanceStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<bool> RecalculateCurrentUidsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        var folderMaximumUids = new List<(long FolderId, long MessageUid)>();

        await using (var command = new SqlCommand(ReadFolderMaximumUidsSql, connection))
        await using (var reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                folderMaximumUids.Add(
                    (Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                     Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture)));
            }
        }

        foreach (var (folderId, messageUid) in folderMaximumUids)
        {
            if (folderId <= 0 || messageUid <= 0)
            {
                return false;
            }

            await using var command = new SqlCommand(AdvanceFolderUidSql, connection);
            command.Parameters.Add("@MessageUid", SqlDbType.BigInt).Value = messageUid;
            command.Parameters.Add("@MessageFolderId", SqlDbType.BigInt).Value = folderId;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
