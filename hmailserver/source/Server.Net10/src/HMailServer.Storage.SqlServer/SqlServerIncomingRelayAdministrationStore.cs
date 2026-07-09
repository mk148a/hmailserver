using System.Data;
using System.Net;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerIncomingRelayAdministrationStore : IIncomingRelayAdministrationStore
{
    public const string GetIncomingRelaysSql = """
SELECT
    relayid,
    relayname,
    relaylowerip1,
    relaylowerip2,
    relayupperip1,
    relayupperip2
FROM hm_incoming_relays
ORDER BY relayname ASC;
""";

    public const string DeleteIncomingRelayByIdSql = """
DELETE FROM hm_incoming_relays
WHERE relayid = @id;
""";

    public const string UpdateIncomingRelaySql = """
UPDATE hm_incoming_relays
SET relayname = @name,
    relaylowerip1 = @lowerIp1,
    relaylowerip2 = @lowerIp2,
    relayupperip1 = @upperIp1,
    relayupperip2 = @upperIp2
WHERE relayid = @id;
""";

    public const string InsertIncomingRelaySql = """
INSERT INTO hm_incoming_relays
    (relayname, relaylowerip1, relaylowerip2, relayupperip1, relayupperip2)
OUTPUT INSERTED.relayid
VALUES (@name, @lowerIp1, @lowerIp2, @upperIp1, @upperIp2);
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerIncomingRelayAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<IncomingRelayAdministrationSnapshot>> GetIncomingRelaysAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetIncomingRelaysSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var relays = new List<IncomingRelayAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var lowerAddress1 = reader.GetInt64(2);
            var lowerAddress2 = reader.IsDBNull(3) ? null : (long?)reader.GetInt64(3);
            var upperAddress1 = reader.GetInt64(4);
            var upperAddress2 = reader.IsDBNull(5) ? null : (long?)reader.GetInt64(5);

            relays.Add(
                new IncomingRelayAdministrationSnapshot(
                    Id: id,
                    Name: name,
                    LowerIp: FormatLegacyAddress(lowerAddress1, lowerAddress2),
                    UpperIp: FormatLegacyAddress(upperAddress1, upperAddress2)));
        }

        return relays;
    }

    public async ValueTask DeleteIncomingRelayByIdAsync(
        int databaseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteIncomingRelayByIdSql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = databaseId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask UpdateIncomingRelayAsync(
        IncomingRelayAdministrationSnapshot relay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relay);

        var lowerIp = ParseLegacyAddress(relay.LowerIp);
        var upperIp = ParseLegacyAddress(relay.UpperIp);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateIncomingRelaySql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = relay.Id;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = relay.Name;
        AddLegacyAddressParameters(command, "@lowerIp1", "@lowerIp2", lowerIp);
        AddLegacyAddressParameters(command, "@upperIp1", "@upperIp2", upperIp);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> InsertIncomingRelayAsync(
        IncomingRelayAdministrationSnapshot relay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relay);

        var lowerIp = ParseLegacyAddress(relay.LowerIp);
        var upperIp = ParseLegacyAddress(relay.UpperIp);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertIncomingRelaySql, connection);
        command.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = relay.Name;
        AddLegacyAddressParameters(command, "@lowerIp1", "@lowerIp2", lowerIp);
        AddLegacyAddressParameters(command, "@upperIp1", "@upperIp2", upperIp);
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, System.Globalization.CultureInfo.InvariantCulture);
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
            throw new FormatException("Incoming relay IP address must be a valid IPv4 or IPv6 address.");
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
            _ => throw new FormatException("Incoming relay IP address must be a valid IPv4 or IPv6 address.")
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
