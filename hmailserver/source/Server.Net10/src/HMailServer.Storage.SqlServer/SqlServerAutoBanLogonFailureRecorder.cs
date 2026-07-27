using System.Data;
using System.Globalization;
using System.Net;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerAutoBanLogonFailureRecorder : IAutoBanLogonFailureRecorder
{
    public const string SelectAutoBanSettingsSql = """
SELECT settingname, settinginteger
FROM hm_settings
WHERE settingname IN (
    N'AutoBanOnLogonFailureEnabled',
    N'MaxInvalidLogonAttempts',
    N'LogonAttemptsWithinMinutes',
    N'AutoBanMinutes');
""";

    public const string CountFailuresSql = """
SELECT CONVERT(int, COUNT_BIG(*))
FROM hm_logon_failures WITH (UPDLOCK, HOLDLOCK)
WHERE ipaddress1 = @IpAddress1
  AND ((@IpAddress2 IS NULL AND ipaddress2 IS NULL) OR ipaddress2 = @IpAddress2);
""";

    public const string InsertFailureSql = """
INSERT INTO hm_logon_failures
(
    ipaddress1,
    ipaddress2,
    failuretime
)
VALUES
(
    @IpAddress1,
    @IpAddress2,
    SYSUTCDATETIME()
);
""";

    public const string ClearFailuresByIpSql = """
DELETE FROM hm_logon_failures
WHERE ipaddress1 = @IpAddress1
  AND ((@IpAddress2 IS NULL AND ipaddress2 IS NULL) OR ipaddress2 = @IpAddress2);
""";

    public const string ClearOldFailuresSql = """
DELETE FROM hm_logon_failures
WHERE failuretime < DATEADD(minute, -@LogonAttemptsWithinMinutes, SYSUTCDATETIME());
""";

    public const string InsertAutoBanRangeSql = """
INSERT INTO hm_securityranges
(
    rangepriorityid,
    rangelowerip1,
    rangelowerip2,
    rangeupperip1,
    rangeupperip2,
    rangeoptions,
    rangename,
    rangeexpires,
    rangeexpirestime
)
VALUES
(
    100,
    @IpAddress1,
    @IpAddress2,
    @IpAddress1,
    @IpAddress2,
    0,
    @RangeName,
    1,
    DATEADD(minute, @AutoBanMinutes, SYSUTCDATETIME())
);
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerAutoBanLogonFailureRecorder(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<AutoBanLogonFailureResult> RecordFailureAsync(
        IPAddress clientAddress,
        string username,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clientAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var settings = await LoadSettingsAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled || settings.MaxInvalidLogonAttempts <= 0)
        {
            return new AutoBanLogonFailureResult(
                Enabled: settings.Enabled,
                FailureCount: 0,
                Disconnect: false,
                RangeCreated: false);
        }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ClearOldFailuresAsync(connection, transaction, settings, cancellationToken).ConfigureAwait(false);
            var ipParts = SqlServerIpAddressParts.From(clientAddress);
            var failureCount = await CountFailuresAsync(connection, transaction, ipParts, cancellationToken).ConfigureAwait(false) + 1;
            if (failureCount >= settings.MaxInvalidLogonAttempts)
            {
                await ClearFailuresByIpAsync(connection, transaction, ipParts, cancellationToken).ConfigureAwait(false);
                var rangeCreated = false;
                if (settings.AutoBanMinutes > 0)
                {
                    var rangeName = await CreateUniqueRangeNameAsync(connection, transaction, username, cancellationToken)
                        .ConfigureAwait(false);
                    await InsertAutoBanRangeAsync(
                        connection,
                        transaction,
                        ipParts,
                        rangeName,
                        settings.AutoBanMinutes,
                        cancellationToken).ConfigureAwait(false);
                    rangeCreated = true;
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new AutoBanLogonFailureResult(
                    Enabled: true,
                    FailureCount: failureCount,
                    Disconnect: true,
                    RangeCreated: rangeCreated);
            }

            await InsertFailureAsync(connection, transaction, ipParts, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new AutoBanLogonFailureResult(
                Enabled: true,
                FailureCount: failureCount,
                Disconnect: false,
                RangeCreated: false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask ClearOldFailuresAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var settings = await LoadSettingsAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            return;
        }

        await using var command = new SqlCommand(ClearOldFailuresSql, connection);
        command.Parameters.Add("@LogonAttemptsWithinMinutes", SqlDbType.Int).Value =
            Math.Max(settings.LogonAttemptsWithinMinutes, 1);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static string BuildRangeNameCandidate(string username, int attempt)
    {
        var sanitized = username.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        if (sanitized.Length == 0)
        {
            sanitized = "unknown";
        }

        var suffix = attempt == 0 ? string.Empty : $" ({attempt.ToString(CultureInfo.InvariantCulture)})";
        var candidate = "Auto-ban: " + sanitized + suffix;
        return candidate.Length <= 100 ? candidate : candidate[..100];
    }

    private static async ValueTask<AutoBanSettings> LoadSettingsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var enabled = true;
        var maxAttempts = 0;
        var withinMinutes = 30;
        var autoBanMinutes = 60;

        await using var command = new SqlCommand(SelectAutoBanSettingsSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.GetString(0);
            var value = reader.GetInt32(1);
            switch (name)
            {
                case "AutoBanOnLogonFailureEnabled":
                    enabled = value != 0;
                    break;
                case "MaxInvalidLogonAttempts":
                    maxAttempts = value;
                    break;
                case "LogonAttemptsWithinMinutes":
                    withinMinutes = value;
                    break;
                case "AutoBanMinutes":
                    autoBanMinutes = value;
                    break;
            }
        }

        return new AutoBanSettings(
            enabled,
            maxAttempts,
            Math.Max(withinMinutes, 1),
            Math.Max(autoBanMinutes, 0));
    }

    private static async ValueTask ClearOldFailuresAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AutoBanSettings settings,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(ClearOldFailuresSql, connection, transaction);
        command.Parameters.Add("@LogonAttemptsWithinMinutes", SqlDbType.Int).Value =
            settings.LogonAttemptsWithinMinutes;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<int> CountFailuresAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SqlServerIpAddressParts ipParts,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(CountFailuresSql, connection, transaction);
        AddIpParameters(command, ipParts);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is int count ? count : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async ValueTask InsertFailureAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SqlServerIpAddressParts ipParts,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertFailureSql, connection, transaction);
        AddIpParameters(command, ipParts);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask ClearFailuresByIpAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SqlServerIpAddressParts ipParts,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(ClearFailuresByIpSql, connection, transaction);
        AddIpParameters(command, ipParts);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<string> CreateUniqueRangeNameAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string username,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= 10; attempt++)
        {
            var candidate = BuildRangeNameCandidate(username, attempt);
            if (!await RangeNameExistsAsync(connection, transaction, candidate, cancellationToken).ConfigureAwait(false))
            {
                return candidate;
            }
        }

        var fallback = "Auto-ban: " + Guid.NewGuid().ToString("N");
        return fallback.Length <= 100 ? fallback : fallback[..100];
    }

    private static async ValueTask<bool> RangeNameExistsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string rangeName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT 1 FROM hm_securityranges WHERE rangename = @RangeName;",
            connection,
            transaction);
        command.Parameters.Add("@RangeName", SqlDbType.NVarChar, 100).Value = rangeName;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    private static async ValueTask InsertAutoBanRangeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SqlServerIpAddressParts ipParts,
        string rangeName,
        int autoBanMinutes,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(InsertAutoBanRangeSql, connection, transaction);
        AddIpParameters(command, ipParts);
        command.Parameters.Add("@RangeName", SqlDbType.NVarChar, 100).Value = rangeName;
        command.Parameters.Add("@AutoBanMinutes", SqlDbType.Int).Value = autoBanMinutes;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddIpParameters(
        SqlCommand command,
        SqlServerIpAddressParts ipParts)
    {
        command.Parameters.Add("@IpAddress1", SqlDbType.BigInt).Value = ipParts.Address1;
        command.Parameters.Add("@IpAddress2", SqlDbType.BigInt).Value =
            ipParts.Address2 is { } address2 ? address2 : DBNull.Value;
    }

    private sealed record SqlServerIpAddressParts(long Address1, long? Address2)
    {
        public static SqlServerIpAddressParts From(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4)
            {
                return new SqlServerIpAddressParts(
                    ((long)bytes[0] << 24) |
                    ((long)bytes[1] << 16) |
                    ((long)bytes[2] << 8) |
                    bytes[3],
                    null);
            }

            if (bytes.Length != 16)
            {
                throw new NotSupportedException($"Unsupported IP address length {bytes.Length}.");
            }

            return new SqlServerIpAddressParts(
                ToSignedInt64(bytes.AsSpan(0, 8)),
                ToSignedInt64(bytes.AsSpan(8, 8)));
        }

        private static long ToSignedInt64(ReadOnlySpan<byte> bytes)
        {
            ulong value = 0;
            for (var i = 0; i < bytes.Length; i++)
            {
                value = (value << 8) | bytes[i];
            }

            return unchecked((long)value);
        }
    }
}
