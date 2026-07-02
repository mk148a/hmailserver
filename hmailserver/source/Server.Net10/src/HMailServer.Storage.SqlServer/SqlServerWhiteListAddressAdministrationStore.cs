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
}
