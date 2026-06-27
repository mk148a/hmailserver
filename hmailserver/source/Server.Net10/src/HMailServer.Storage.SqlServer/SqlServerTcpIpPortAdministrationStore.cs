using System.Data;
using System.Globalization;
using System.Net;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerTcpIpPortAdministrationStore : ITcpIpPortAdministrationStore
{
    public const string GetTcpIpPortsSql = """
SELECT
    portid,
    portprotocol,
    portnumber,
    portaddress1,
    portaddress2,
    portconnectionsecurity,
    portsslcertificateid
FROM hm_tcpipports
ORDER BY portaddress1 ASC, portaddress2 ASC, portnumber ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerTcpIpPortAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<TcpIpPortAdministrationSnapshot>> GetTcpIpPortsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetTcpIpPortsSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var ports = new List<TcpIpPortAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt32(0);
            var protocol = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            var portNumber = reader.GetInt32(2);
            var address1 = reader.GetInt64(3);
            var address2 = reader.IsDBNull(4) ? null : (long?)reader.GetInt64(4);
            var connectionSecurity = Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture);
            var sslCertificateId = Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture);

            ports.Add(
                new TcpIpPortAdministrationSnapshot(
                    Id: id,
                    Protocol: protocol,
                    PortNumber: portNumber,
                    Address: FormatLegacyAddress(address1, address2),
                    ConnectionSecurity: connectionSecurity,
                    SslCertificateId: sslCertificateId));
        }

        return ports;
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
