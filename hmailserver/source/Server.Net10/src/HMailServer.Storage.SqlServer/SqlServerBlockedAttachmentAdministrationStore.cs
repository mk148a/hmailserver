using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerBlockedAttachmentAdministrationStore : IBlockedAttachmentAdministrationStore
{
    public const string GetBlockedAttachmentsSql = """
SELECT
    baid,
    bawildcard,
    badescription
FROM hm_blocked_attachments
ORDER BY bawildcard ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerBlockedAttachmentAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>> GetBlockedAttachmentsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetBlockedAttachmentsSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var blockedAttachments = new List<BlockedAttachmentAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            blockedAttachments.Add(
                new BlockedAttachmentAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    Wildcard: reader.GetString(1),
                    Description: reader.GetString(2)));
        }

        return blockedAttachments;
    }
}
