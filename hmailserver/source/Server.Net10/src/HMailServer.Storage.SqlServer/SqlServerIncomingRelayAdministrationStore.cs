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
