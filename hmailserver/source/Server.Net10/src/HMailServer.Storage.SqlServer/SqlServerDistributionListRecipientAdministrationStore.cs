using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDistributionListRecipientAdministrationStore
    : IDistributionListRecipientAdministrationStore
{
    public const string GetRecipientsSql = """
SELECT
    distributionlistrecipientid,
    distributionlistrecipientlistid,
    distributionlistrecipientaddress
FROM hm_distributionlistsrecipients
WHERE distributionlistrecipientlistid = @DistributionListID
ORDER BY distributionlistrecipientaddress ASC;
""";

    public const string InsertDistributionListRecipientSql = """
INSERT INTO hm_distributionlistsrecipients
    (distributionlistrecipientlistid, distributionlistrecipientaddress)
OUTPUT INSERTED.distributionlistrecipientid
VALUES (@ListId, @Address);
""";

    public const string UpdateDistributionListRecipientSql = """
UPDATE hm_distributionlistsrecipients
SET
    distributionlistrecipientlistid = @ListId,
    distributionlistrecipientaddress = @Address
WHERE distributionlistrecipientid = @ID
  AND distributionlistrecipientlistid = @ListId;
""";

    public const string DeleteDistributionListRecipientSql = """
DELETE FROM hm_distributionlistsrecipients
WHERE distributionlistrecipientid = @ID
  AND distributionlistrecipientlistid = @ListId;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDistributionListRecipientAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(
        int distributionListId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetRecipientsSql, connection);
        command.Parameters.Add("@DistributionListID", SqlDbType.Int).Value = distributionListId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var recipients = new List<DistributionListRecipientAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            recipients.Add(
                new DistributionListRecipientAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    ListId: reader.GetInt32(1),
                    Address: reader.GetString(2)));
        }

        return recipients;
    }

    public async ValueTask<int> InsertDistributionListRecipientAsync(
        DistributionListRecipientAdministrationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertDistributionListRecipientSql, connection);
        command.Parameters.Add("@ListId", SqlDbType.Int).Value = snapshot.ListId;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = snapshot.Address;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask<bool> UpdateDistributionListRecipientAsync(
        DistributionListRecipientAdministrationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateDistributionListRecipientSql, connection);
        command.Parameters.Add("@ID", SqlDbType.Int).Value = snapshot.Id;
        command.Parameters.Add("@ListId", SqlDbType.Int).Value = snapshot.ListId;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = snapshot.Address;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<bool> DeleteDistributionListRecipientAsync(
        DistributionListRecipientAdministrationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteDistributionListRecipientSql, connection);
        command.Parameters.Add("@ID", SqlDbType.Int).Value = snapshot.Id;
        command.Parameters.Add("@ListId", SqlDbType.Int).Value = snapshot.ListId;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }
}
