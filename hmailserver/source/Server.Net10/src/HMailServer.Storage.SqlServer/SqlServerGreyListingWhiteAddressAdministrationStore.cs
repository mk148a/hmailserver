using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerGreyListingWhiteAddressAdministrationStore
    : IGreyListingWhiteAddressAdministrationStore
{
    public const string GetWhiteAddressesSql = """
SELECT
    whiteid,
    whiteipaddress,
    whiteipdescription
FROM hm_greylisting_whiteaddresses
ORDER BY whiteipaddress ASC;
""";

    public const string InsertWhiteAddressSql = """
INSERT INTO hm_greylisting_whiteaddresses
    (whiteipaddress, whiteipdescription)
OUTPUT INSERTED.whiteid
VALUES (@ipAddress, @description);
""";

    public const string UpdateWhiteAddressSql = """
UPDATE hm_greylisting_whiteaddresses
SET whiteipaddress = @ipAddress,
    whiteipdescription = @description
WHERE whiteid = @id;
""";

    public const string DeleteWhiteAddressSql = """
DELETE FROM hm_greylisting_whiteaddresses
WHERE whiteid = @id;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerGreyListingWhiteAddressAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>> GetWhiteAddressesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetWhiteAddressesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var addresses = new List<GreyListingWhiteAddressAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            addresses.Add(
                new GreyListingWhiteAddressAdministrationSnapshot(
                    Id: reader.GetInt64(0),
                    StoredIpAddress: reader.GetString(1),
                    Description: reader.GetString(2)));
        }

        return addresses;
    }

    public async ValueTask<long> InsertWhiteAddressAsync(
        GreyListingWhiteAddressAdministrationSnapshot address,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertWhiteAddressSql, connection);
        command.Parameters.Add("@ipAddress", SqlDbType.NVarChar, 255).Value = address.StoredIpAddress;
        command.Parameters.Add("@description", SqlDbType.NVarChar, 255).Value = address.Description;

        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(insertedId, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask<bool> UpdateWhiteAddressAsync(
        GreyListingWhiteAddressAdministrationSnapshot address,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateWhiteAddressSql, connection);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = address.Id;
        command.Parameters.Add("@ipAddress", SqlDbType.NVarChar, 255).Value = address.StoredIpAddress;
        command.Parameters.Add("@description", SqlDbType.NVarChar, 255).Value = address.Description;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<bool> DeleteWhiteAddressByIdAsync(
        long databaseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteWhiteAddressSql, connection);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = databaseId;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }
}
