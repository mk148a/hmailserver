using System.Net;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerAutoBanLogonFailureRecorderIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable =
        "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task RecordFailureAsync_UsesLegacyThresholdAndDisconnectBranchesAgainstIsolatedDatabase()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();

        var databaseName = $"hmailserver_net10_autoban_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);

            var recorder = new SqlServerAutoBanLogonFailureRecorder(
                new SqlServerConnectionFactory(testConnectionString));
            var clientAddress = IPAddress.Parse("198.51.100.27");

            var first = await recorder
                .RecordFailureAsync(clientAddress, "user@example.test", CancellationToken.None)
                .ConfigureAwait(false);
            var second = await recorder
                .RecordFailureAsync(clientAddress, "user@example.test", CancellationToken.None)
                .ConfigureAwait(false);
            var threshold = await recorder
                .RecordFailureAsync(clientAddress, "user@example.test", CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsTrue(first.Enabled);
            Assert.AreEqual(1, first.FailureCount);
            Assert.IsFalse(first.Disconnect);
            Assert.IsFalse(first.RangeCreated);
            Assert.AreEqual(2, second.FailureCount);
            Assert.IsFalse(second.Disconnect);
            Assert.IsFalse(second.RangeCreated);
            Assert.AreEqual(3, threshold.FailureCount);
            Assert.IsTrue(threshold.Disconnect);
            Assert.IsTrue(threshold.RangeCreated);
            Assert.AreEqual(0, await CountFailuresAsync(testConnectionString, clientAddress).ConfigureAwait(false));

            var range = await ReadRangeAsync(testConnectionString, "Auto-ban: user@example.test").ConfigureAwait(false);
            Assert.IsNotNull(range);
            Assert.AreEqual(100, range.Priority);
            Assert.AreEqual(ToIpv4Value(clientAddress), range.LowerAddress1);
            Assert.IsNull(range.LowerAddress2);
            Assert.AreEqual(ToIpv4Value(clientAddress), range.UpperAddress1);
            Assert.IsNull(range.UpperAddress2);
            Assert.AreEqual(0, range.Options);
            Assert.AreEqual(1, range.Expires);

            await UpdateSettingsAsync(testConnectionString, autoBanMinutes: 0, maxInvalidLogonAttempts: 1)
                .ConfigureAwait(false);
            var disconnectOnlyAddress = IPAddress.Parse("203.0.113.19");
            var service = new ClientAwareAuthenticationService(
                new FailingAuthenticator(),
                recorder);
            var disconnectOnly = await service
                .AuthenticateAsync(
                    new ClientAuthenticationRequest(
                        "disconnect@example.test",
                        "wrong",
                        disconnectOnlyAddress,
                        ClientAuthenticationCaller.Imap),
                    CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsFalse(disconnectOnly.Authentication.Succeeded);
            Assert.IsTrue(disconnectOnly.Disconnect);
            Assert.AreEqual(0, await CountFailuresAsync(testConnectionString, disconnectOnlyAddress).ConfigureAwait(false));
            Assert.IsNull(
                await ReadRangeAsync(testConnectionString, "Auto-ban: disconnect@example.test")
                    .ConfigureAwait(false));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        }
    }

    private static string GetApprovedConnectionStringOrInconclusive()
    {
        var rawConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var allowDatabaseCreate = Environment.GetEnvironmentVariable(AllowDatabaseCreateEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(rawConnectionString) ||
            !string.Equals(allowDatabaseCreate, "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {ConnectionEnvironmentVariable} to a disposable local SQL target and " +
                $"{AllowDatabaseCreateEnvironmentVariable}=1 to run this destructive fixture.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(rawConnectionString);
        }
        catch (ArgumentException exception)
        {
            Assert.Inconclusive($"The SQL integration connection string is invalid: {exception.Message}");
            throw;
        }

        if (!IsApprovedLocalDataSource(builder.DataSource) ||
            !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            Assert.Inconclusive(
                "The SQL integration fixture only accepts a local SQL/LocalDB target without AttachDbFilename.");
        }

        return builder.ConnectionString;
    }

    private static bool IsApprovedLocalDataSource(string dataSource)
    {
        var normalized = dataSource.Trim();
        return normalized.Equals(".", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("(local)", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(".\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("localhost\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase);
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand($"CREATE DATABASE [{databaseName}];", connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
CREATE TABLE dbo.hm_settings
(
    settingname nvarchar(255) NOT NULL PRIMARY KEY,
    settingstring nvarchar(max) NOT NULL,
    settinginteger int NOT NULL
);

CREATE TABLE dbo.hm_logon_failures
(
    ipaddress1 bigint NOT NULL,
    ipaddress2 bigint NULL,
    failuretime datetime NOT NULL
);

CREATE TABLE dbo.hm_securityranges
(
    rangeid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    rangepriorityid int NOT NULL,
    rangelowerip1 bigint NOT NULL,
    rangelowerip2 bigint NULL,
    rangeupperip1 bigint NOT NULL,
    rangeupperip2 bigint NULL,
    rangeoptions int NOT NULL,
    rangename nvarchar(100) NOT NULL UNIQUE,
    rangeexpires tinyint NOT NULL,
    rangeexpirestime datetime NOT NULL
);

INSERT INTO dbo.hm_settings (settingname, settingstring, settinginteger)
VALUES
    (N'AutoBanOnLogonFailureEnabled', N'', 1),
    (N'MaxInvalidLogonAttempts', N'', 3),
    (N'LogonAttemptsWithinMinutes', N'', 30),
    (N'AutoBanMinutes', N'', 60);
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task UpdateSettingsAsync(
        string connectionString,
        int autoBanMinutes,
        int maxInvalidLogonAttempts)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand("""
UPDATE dbo.hm_settings
SET settinginteger = CASE settingname
    WHEN N'AutoBanMinutes' THEN @AutoBanMinutes
    WHEN N'MaxInvalidLogonAttempts' THEN @MaxInvalidLogonAttempts
    ELSE settinginteger
END
WHERE settingname IN (N'AutoBanMinutes', N'MaxInvalidLogonAttempts');
""", connection);
        command.Parameters.AddWithValue("@AutoBanMinutes", autoBanMinutes);
        command.Parameters.AddWithValue("@MaxInvalidLogonAttempts", maxInvalidLogonAttempts);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<long> CountFailuresAsync(string connectionString, IPAddress clientAddress)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand("""
SELECT COUNT_BIG(*)
FROM dbo.hm_logon_failures
WHERE ipaddress1 = @IpAddress1
  AND ipaddress2 IS NULL;
""", connection);
        command.Parameters.AddWithValue("@IpAddress1", ToIpv4Value(clientAddress));
        return Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false));
    }

    private static async Task<AutoBanRangeSnapshot?> ReadRangeAsync(
        string connectionString,
        string rangeName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand("""
SELECT rangepriorityid, rangelowerip1, rangelowerip2,
       rangeupperip1, rangeupperip2, rangeoptions, rangeexpires
FROM dbo.hm_securityranges
WHERE rangename = @RangeName;
""", connection);
        command.Parameters.AddWithValue("@RangeName", rangeName);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            return null;
        }

        return new AutoBanRangeSnapshot(
            reader.GetInt32(0),
            reader.GetInt64(1),
            reader.IsDBNull(2) ? null : reader.GetInt64(2),
            reader.GetInt64(3),
            reader.IsDBNull(4) ? null : reader.GetInt64(4),
            reader.GetInt32(5),
            reader.GetByte(6));
    }

    private static long ToIpv4Value(IPAddress clientAddress)
    {
        var bytes = clientAddress.GetAddressBytes();
        Assert.AreEqual(4, bytes.Length);
        return ((long)bytes[0] << 24) |
            ((long)bytes[1] << 16) |
            ((long)bytes[2] << 8) |
            bytes[3];
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];",
            connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private sealed record AutoBanRangeSnapshot(
        int Priority,
        long LowerAddress1,
        long? LowerAddress2,
        long UpperAddress1,
        long? UpperAddress2,
        int Options,
        byte Expires);

    private sealed class FailingAuthenticator : IImapAccountAuthenticator
    {
        public ValueTask<ImapAuthenticationResult> AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(ImapAuthenticationResult.Failure("Invalid user name or password."));
    }
}
