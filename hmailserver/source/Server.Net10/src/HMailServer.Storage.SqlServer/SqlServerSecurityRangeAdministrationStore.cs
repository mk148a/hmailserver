using System.Data;
using System.Globalization;
using System.Net;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSecurityRangeAdministrationStore : ISecurityRangeAdministrationStore
{
    public const string GetSecurityRangesSql = """
SELECT
    rangeid,
    rangename,
    rangepriorityid,
    rangelowerip1,
    rangelowerip2,
    rangeupperip1,
    rangeupperip2,
    rangeoptions,
    rangeexpires,
    rangeexpirestime
FROM hm_securityranges
ORDER BY rangeexpires ASC, rangepriorityid DESC, rangename ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerSecurityRangeAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<SecurityRangeAdministrationSnapshot>> GetSecurityRangesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetSecurityRangesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var ranges = new List<SecurityRangeAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var priority = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture);
            var lowerIp1 = reader.GetInt64(3);
            var lowerIp2 = reader.IsDBNull(4) ? null : (long?)reader.GetInt64(4);
            var upperIp1 = reader.GetInt64(5);
            var upperIp2 = reader.IsDBNull(6) ? null : (long?)reader.GetInt64(6);
            var options = Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture);
            var expires = Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture) == 1;
            var expiresTime = reader.GetDateTime(9);

            ranges.Add(
                new SecurityRangeAdministrationSnapshot(
                    Id: id,
                    Name: name,
                    LowerIp: FormatLegacyAddress(lowerIp1, lowerIp2),
                    UpperIp: FormatLegacyAddress(upperIp1, upperIp2),
                    Priority: priority,
                    Options: options,
                    Expires: expires,
                    ExpiresTime: expiresTime));
        }

        return ranges;
    }

    private static string FormatLegacyAddress(long address1, long? address2)
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
