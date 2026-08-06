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

    public const string InsertTcpIpPortSql = """
INSERT INTO hm_tcpipports
    (portprotocol, portnumber, portaddress1, portaddress2, portconnectionsecurity, portsslcertificateid)
OUTPUT INSERTED.portid
VALUES (@protocol, @portNumber, @address1, @address2, @connectionSecurity, @sslCertificateId);
""";

    public const string DeleteTcpIpPortByIdSql = """
DELETE FROM hm_tcpipports
WHERE portid = @id;
""";

    public const string UpdateTcpIpPortSql = """
UPDATE hm_tcpipports
SET portprotocol = @protocol,
    portnumber = @portNumber,
    portaddress1 = @address1,
    portaddress2 = @address2,
    portconnectionsecurity = @connectionSecurity,
    portsslcertificateid = @sslCertificateId
WHERE portid = @id;
""";

    public const string DeleteAllTcpIpPortsSql = """
        DELETE FROM hm_tcpipports;
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

    public async ValueTask DeleteAllTcpIpPortsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteAllTcpIpPortsSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask<int> InsertTcpIpPortAsync(
        TcpIpPortAdministrationSnapshot port,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(port);
        var address = ParseLegacyAddress(port.Address);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertTcpIpPortSql, connection);
        command.Parameters.Add("@protocol", SqlDbType.TinyInt).Value = port.Protocol;
        command.Parameters.Add("@portNumber", SqlDbType.Int).Value = port.PortNumber;
        command.Parameters.Add("@address1", SqlDbType.BigInt).Value = address.Address1;
        command.Parameters.Add("@address2", SqlDbType.BigInt).Value =
            address.Address2.HasValue ? address.Address2.Value : DBNull.Value;
        command.Parameters.Add("@connectionSecurity", SqlDbType.TinyInt).Value = port.ConnectionSecurity;
        command.Parameters.Add("@sslCertificateId", SqlDbType.BigInt).Value = port.SslCertificateId;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }

    public async ValueTask DeleteTcpIpPortByIdAsync(
        int databaseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteTcpIpPortByIdSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = databaseId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask UpdateTcpIpPortAsync(
        TcpIpPortAdministrationSnapshot port,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(port);
        var address = ParseLegacyAddress(port.Address);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateTcpIpPortSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = port.Id;
        command.Parameters.Add("@protocol", SqlDbType.TinyInt).Value = port.Protocol;
        command.Parameters.Add("@portNumber", SqlDbType.Int).Value = port.PortNumber;
        command.Parameters.Add("@address1", SqlDbType.BigInt).Value = address.Address1;
        command.Parameters.Add("@address2", SqlDbType.BigInt).Value =
            address.Address2.HasValue ? address.Address2.Value : DBNull.Value;
        command.Parameters.Add("@connectionSecurity", SqlDbType.TinyInt).Value = port.ConnectionSecurity;
        command.Parameters.Add("@sslCertificateId", SqlDbType.BigInt).Value = port.SslCertificateId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

    private static LegacyAddressParts ParseLegacyAddress(string address)
    {
        if (!IPAddress.TryParse(address, out var ipAddress))
        {
            throw new FormatException("TCP/IP port address must be a valid IPv4 or IPv6 address.");
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
            _ => throw new FormatException("TCP/IP port address must be a valid IPv4 or IPv6 address.")
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

    private sealed record LegacyAddressParts(long Address1, long? Address2);
}
