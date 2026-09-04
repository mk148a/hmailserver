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

    public const string InsertSecurityRangeSql = """
INSERT INTO hm_securityranges
    (rangename, rangepriorityid, rangelowerip1, rangelowerip2, rangeupperip1, rangeupperip2, rangeoptions, rangeexpires, rangeexpirestime)
OUTPUT INSERTED.rangeid
VALUES (@name, @priority, @lowerIp1, @lowerIp2, @upperIp1, @upperIp2, @options, @expires, @expiresTime);
""";

    public const string UpdateSecurityRangeSql = """
UPDATE hm_securityranges
SET rangename = @name,
    rangepriorityid = @priority,
    rangelowerip1 = @lowerIp1,
    rangelowerip2 = @lowerIp2,
    rangeupperip1 = @upperIp1,
    rangeupperip2 = @upperIp2,
    rangeoptions = @options,
    rangeexpires = @expires,
    rangeexpirestime = @expiresTime
WHERE rangeid = @id;
""";

    public const string DeleteSecurityRangeByIdSql = """
DELETE FROM hm_securityranges
WHERE rangeid = @id;
""";

    public const string DeleteAllSecurityRangesForRestoreSql = """
DELETE FROM hm_securityranges;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerSecurityRangeAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerSecurityRangeAdministrationStore(
        SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _connectionFactory = null!;
        _transactionContext = transactionContext;
    }

    public async ValueTask<IReadOnlyList<SecurityRangeAdministrationSnapshot>> GetSecurityRangesAsync(
        CancellationToken cancellationToken)
    {
        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            GetSecurityRangesSql,
            cancellationToken).ConfigureAwait(false);
        var command = commandLease.Command;
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

    public async ValueTask<int> InsertSecurityRangeAsync(
        SecurityRangeAdministrationSnapshot range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);

        var name = NormalizeName(range.Name);
        if (name.Length == 0)
        {
            throw new InvalidOperationException("The name cannot be empty.");
        }

        var lowerIp = ParseLegacyAddress(range.LowerIp);
        var upperIp = ParseLegacyAddress(range.UpperIp);
        ValidateRange(lowerIp, upperIp);

        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            InsertSecurityRangeSql,
            cancellationToken).ConfigureAwait(false);
        var command = commandLease.Command;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 100).Value = name;
        command.Parameters.Add("@priority", SqlDbType.Int).Value = range.Priority;
        AddLegacyAddressParameters(command, "@lowerIp1", "@lowerIp2", lowerIp);
        AddLegacyAddressParameters(command, "@upperIp1", "@upperIp2", upperIp);
        command.Parameters.Add("@options", SqlDbType.Int).Value = range.Options;
        command.Parameters.Add("@expires", SqlDbType.TinyInt).Value = range.Expires ? 1 : 0;
        command.Parameters.Add("@expiresTime", SqlDbType.DateTime).Value = NormalizeExpiresTime(range.ExpiresTime);

        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }

    public async ValueTask UpdateSecurityRangeAsync(
        SecurityRangeAdministrationSnapshot range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);

        var name = NormalizeName(range.Name);
        if (name.Length == 0)
        {
            throw new InvalidOperationException("The name cannot be empty.");
        }

        var lowerIp = ParseLegacyAddress(range.LowerIp);
        var upperIp = ParseLegacyAddress(range.UpperIp);
        ValidateRange(lowerIp, upperIp);

        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            UpdateSecurityRangeSql,
            cancellationToken).ConfigureAwait(false);
        var command = commandLease.Command;
        command.Parameters.Add("@id", SqlDbType.Int).Value = range.Id;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 100).Value = name;
        command.Parameters.Add("@priority", SqlDbType.Int).Value = range.Priority;
        AddLegacyAddressParameters(command, "@lowerIp1", "@lowerIp2", lowerIp);
        AddLegacyAddressParameters(command, "@upperIp1", "@upperIp2", upperIp);
        command.Parameters.Add("@options", SqlDbType.Int).Value = range.Options;
        command.Parameters.Add("@expires", SqlDbType.TinyInt).Value = range.Expires ? 1 : 0;
        command.Parameters.Add("@expiresTime", SqlDbType.DateTime).Value = NormalizeExpiresTime(range.ExpiresTime);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteSecurityRangeByIdAsync(
        int databaseId,
        CancellationToken cancellationToken)
    {
        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            DeleteSecurityRangeByIdSql,
            cancellationToken).ConfigureAwait(false);
        var command = commandLease.Command;
        command.Parameters.Add("@id", SqlDbType.Int).Value = databaseId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask DeleteAllSecurityRangesForRestoreAsync(
        CancellationToken cancellationToken)
    {
        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            DeleteAllSecurityRangesForRestoreSql,
            cancellationToken).ConfigureAwait(false);
        await commandLease.Command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

    private static string NormalizeName(string name)
    {
        var normalized = name ?? string.Empty;
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }

    private static DateTime NormalizeExpiresTime(DateTime expiresTime) =>
        expiresTime < new DateTime(1753, 1, 1)
            ? new DateTime(2001, 1, 1)
            : expiresTime;

    private static LegacyAddressParts ParseLegacyAddress(string address)
    {
        if (!IPAddress.TryParse(address, out var ipAddress))
        {
            throw new FormatException("Security range IP address must be a valid IPv4 or IPv6 address.");
        }

        var bytes = ipAddress.GetAddressBytes();
        return bytes.Length switch
        {
            4 => new LegacyAddressParts(
                ((long)bytes[0] << 24)
                | ((long)bytes[1] << 16)
                | ((long)bytes[2] << 8)
                | bytes[3],
                null,
                bytes),
            16 => new LegacyAddressParts(
                ReadInt64BigEndian(bytes, 0),
                ReadInt64BigEndian(bytes, 8),
                bytes),
            _ => throw new FormatException("Security range IP address must be a valid IPv4 or IPv6 address.")
        };
    }

    private static void ValidateRange(LegacyAddressParts lowerIp, LegacyAddressParts upperIp)
    {
        if (lowerIp.Bytes.Length != upperIp.Bytes.Length)
        {
            throw new InvalidOperationException(
                "The lower IP address and upper IP address must be of the same IP version type.");
        }

        for (var index = 0; index < lowerIp.Bytes.Length; index++)
        {
            if (lowerIp.Bytes[index] < upperIp.Bytes[index])
            {
                return;
            }

            if (lowerIp.Bytes[index] > upperIp.Bytes[index])
            {
                throw new InvalidOperationException(
                    "The lower IP address must be lower or the same as the upper IP address.");
            }
        }
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

    private sealed record LegacyAddressParts(long Address1, long? Address2, byte[] Bytes);
}
