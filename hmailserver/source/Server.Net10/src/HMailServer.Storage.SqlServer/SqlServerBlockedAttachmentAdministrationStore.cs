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

    public const string InsertBlockedAttachmentSql = """
INSERT INTO hm_blocked_attachments
    (bawildcard, badescription)
OUTPUT INSERTED.baid
VALUES (@wildcard, @description);
""";

    public const string UpdateBlockedAttachmentSql = """
UPDATE hm_blocked_attachments
SET bawildcard = @wildcard,
    badescription = @description
WHERE baid = @id;
""";

    public const string DeleteBlockedAttachmentByIdSql = """
DELETE FROM hm_blocked_attachments
WHERE baid = @id;
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

    public async ValueTask<int> InsertBlockedAttachmentAsync(
        BlockedAttachmentAdministrationSnapshot attachment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertBlockedAttachmentSql, connection);
        command.Parameters.Add("@wildcard", SqlDbType.NVarChar, 255).Value = attachment.Wildcard;
        command.Parameters.Add("@description", SqlDbType.NVarChar, 255).Value = attachment.Description;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask UpdateBlockedAttachmentAsync(
        BlockedAttachmentAdministrationSnapshot attachment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateBlockedAttachmentSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = attachment.Id;
        command.Parameters.Add("@wildcard", SqlDbType.NVarChar, 255).Value = attachment.Wildcard;
        command.Parameters.Add("@description", SqlDbType.NVarChar, 255).Value = attachment.Description;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteBlockedAttachmentByIdAsync(
        int databaseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteBlockedAttachmentByIdSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = databaseId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
