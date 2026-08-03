using System.Data;
using System.Net;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerWhiteListAddressAdministrationStore : IWhiteListAddressAdministrationStore
{
    public const string GetWhiteListAddressesSql = """
SELECT
    whiteid,
    whiteloweripaddress1,
    whiteloweripaddress2,
    whiteupperipaddress1,
    whiteupperipaddress2,
    whiteemailaddress,
    whitedescription
    FROM hm_whitelist
    ORDER BY whiteloweripaddress1 ASC, whiteloweripaddress2 ASC;
""";

    public const string InsertWhiteListAddressSql = """
INSERT INTO hm_whitelist
    (whiteloweripaddress1, whiteloweripaddress2, whiteupperipaddress1, whiteupperipaddress2, whiteemailaddress, whitedescription)
OUTPUT INSERTED.whiteid
VALUES (@lowerIp1, @lowerIp2, @upperIp1, @upperIp2, @emailAddress, @description);
""";

    public const string UpdateWhiteListAddressSql = """
UPDATE hm_whitelist
SET whiteloweripaddress1 = @lowerIp1,
    whiteloweripaddress2 = @lowerIp2,
    whiteupperipaddress1 = @upperIp1,
    whiteupperipaddress2 = @upperIp2,
    whiteemailaddress = @emailAddress,
    whitedescription = @description
WHERE whiteid = @id;
""";

    public const string DeleteWhiteListAddressSql = """
DELETE FROM hm_whitelist
WHERE whiteid = @id;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerWhiteListAddressAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<WhiteListAddressAdministrationSnapshot>> GetWhiteListAddressesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetWhiteListAddressesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var addresses = new List<WhiteListAddressAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var lowerAddress1 = reader.GetInt64(1);
            var lowerAddress2 = reader.IsDBNull(2) ? null : (long?)reader.GetInt64(2);
            var upperAddress1 = reader.GetInt64(3);
            var upperAddress2 = reader.IsDBNull(4) ? null : (long?)reader.GetInt64(4);

            addresses.Add(
                new WhiteListAddressAdministrationSnapshot(
                    Id: reader.GetInt64(0),
                    LowerIpAddress: FormatLegacyAddress(lowerAddress1, lowerAddress2),
                    UpperIpAddress: FormatLegacyAddress(upperAddress1, upperAddress2),
                    EmailAddress: reader.GetString(5),
                    Description: reader.GetString(6)));
        }

        return addresses;
    }

    public async ValueTask<long> InsertWhiteListAddressAsync(
        WhiteListAddressAdministrationSnapshot address,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        var lowerIp = ParseLegacyAddress(address.LowerIpAddress);
        var upperIp = ParseLegacyAddress(address.UpperIpAddress);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertWhiteListAddressSql, connection);
        AddLegacyAddressParameters(command, "@lowerIp1", "@lowerIp2", lowerIp);
        AddLegacyAddressParameters(command, "@upperIp1", "@upperIp2", upperIp);
        command.Parameters.Add("@emailAddress", SqlDbType.NVarChar, 255).Value = address.EmailAddress;
        command.Parameters.Add("@description", SqlDbType.NVarChar, 255).Value = address.Description;

        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(insertedId, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask UpdateWhiteListAddressAsync(
        WhiteListAddressAdministrationSnapshot address,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        var lowerIp = ParseLegacyAddress(address.LowerIpAddress);
        var upperIp = ParseLegacyAddress(address.UpperIpAddress);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateWhiteListAddressSql, connection);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = address.Id;
        AddLegacyAddressParameters(command, "@lowerIp1", "@lowerIp2", lowerIp);
        AddLegacyAddressParameters(command, "@upperIp1", "@upperIp2", upperIp);
        command.Parameters.Add("@emailAddress", SqlDbType.NVarChar, 255).Value = address.EmailAddress;
        command.Parameters.Add("@description", SqlDbType.NVarChar, 255).Value = address.Description;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> DeleteWhiteListAddressByIdAsync(
        long databaseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteWhiteListAddressSql, connection);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = databaseId;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    internal static string FormatLegacyAddress(long address1, long? address2)
    {
        if (address2 is null)
        {
            var bytes = new[]
            {
                (byte)((ulong)address1 >> 24),
                (byte)((ulong)address1 >> 16),
                (byte)((ulong)address1 >> 8),
                (byte)(ulong)address1
            };
            return new IPAddress(bytes).ToString();
        }

        var ipv6Bytes = new byte[16];
        WriteInt64BigEndian(address1, ipv6Bytes, offset: 0);
        WriteInt64BigEndian(address2.Value, ipv6Bytes, offset: 8);
        return new IPAddress(ipv6Bytes).ToString();
    }

    private static void WriteInt64BigEndian(long value, byte[] bytes, int offset)
    {
        var unsigned = unchecked((ulong)value);
        for (var index = 0; index < 8; index++)
        {
            bytes[offset + index] = (byte)(unsigned >> ((7 - index) * 8));
        }
    }

    private static LegacyAddressParts ParseLegacyAddress(string address)
    {
        if (!IPAddress.TryParse(address, out var ipAddress))
        {
            throw new FormatException("Whitelist IP address must be a valid IPv4 or IPv6 address.");
        }

        var bytes = ipAddress.GetAddressBytes();
        return bytes.Length switch
        {
            4 => new LegacyAddressParts(
                ((long)bytes[0] << 24)
                | ((long)bytes[1] << 16)
                | ((long)bytes[2] << 8)
                | bytes[3],
                null),
            16 => new LegacyAddressParts(
                ReadInt64BigEndian(bytes, 0),
                ReadInt64BigEndian(bytes, 8)),
            _ => throw new FormatException("Whitelist IP address must be a valid IPv4 or IPv6 address.")
        };
    }

    private static long ReadInt64BigEndian(byte[] bytes, int offset)
    {
        ulong value = 0;
        for (var index = 0; index < 8; index++)
        {
            value = (value << 8) | bytes[offset + index];
        }

        return unchecked((long)value);
    }

    private static void AddLegacyAddressParameters(
        SqlCommand command,
        string address1Parameter,
        string address2Parameter,
        LegacyAddressParts address)
    {
        command.Parameters.Add(address1Parameter, SqlDbType.BigInt).Value = address.Address1;
        command.Parameters.Add(address2Parameter, SqlDbType.BigInt).Value =
            address.Address2.HasValue ? address.Address2.Value : DBNull.Value;
    }

    private sealed record LegacyAddressParts(long Address1, long? Address2);
}
