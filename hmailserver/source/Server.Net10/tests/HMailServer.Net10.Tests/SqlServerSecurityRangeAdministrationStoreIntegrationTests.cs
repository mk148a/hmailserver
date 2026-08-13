using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerSecurityRangeAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task InsertSecurityRange_PersistsGeneratedIdentityAndLegacyColumns()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_securityrange_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);

        try
        {
            await CreateSchemaAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerSecurityRangeAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));
            var expectedExpiry = new DateTime(2026, 12, 31, 23, 59, 58);

            var insertedId = await store.InsertSecurityRangeAsync(
                new SecurityRangeAdministrationSnapshot(
                    Id: 0,
                    Name: "Disposable SQL range",
                    LowerIp: "10.20.30.40",
                    UpperIp: "10.20.30.99",
                    Priority: 27,
                    Options: 12345,
                    Expires: true,
                    ExpiresTime: expectedExpiry),
                CancellationToken.None).ConfigureAwait(false);

            Assert.IsTrue(insertedId > 0);

            await using (var connection = new SqlConnection(testConnectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                await using var command = new SqlCommand(
                    "SELECT rangeid, rangename, rangepriorityid, rangelowerip1, rangelowerip2, rangeupperip1, rangeupperip2, rangeoptions, rangeexpires, rangeexpirestime FROM hm_securityranges WHERE rangeid = @id;",
                    connection);
                command.Parameters.AddWithValue("@id", insertedId);
                await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                Assert.IsTrue(await reader.ReadAsync().ConfigureAwait(false));
                Assert.AreEqual(insertedId, reader.GetInt32(0));
                Assert.AreEqual("Disposable SQL range", reader.GetString(1));
                Assert.AreEqual(27, reader.GetInt32(2));
                Assert.AreEqual(169090600L, reader.GetInt64(3));
                Assert.IsTrue(reader.IsDBNull(4));
                Assert.AreEqual(169090659L, reader.GetInt64(5));
                Assert.IsTrue(reader.IsDBNull(6));
                Assert.AreEqual(12345, reader.GetInt32(7));
                Assert.AreEqual(1, reader.GetByte(8));
                Assert.AreEqual(expectedExpiry, reader.GetDateTime(9));
            }

            var readBack = await store.GetSecurityRangesAsync(CancellationToken.None).ConfigureAwait(false);
            var persisted = readBack.Single(range => range.Id == insertedId);
            Assert.AreEqual("Disposable SQL range", persisted.Name);
            Assert.AreEqual("10.20.30.40", persisted.LowerIp);
            Assert.AreEqual("10.20.30.99", persisted.UpperIp);
            Assert.AreEqual(27, persisted.Priority);
            Assert.AreEqual(12345, persisted.Options);
            Assert.IsTrue(persisted.Expires);
            Assert.AreEqual(expectedExpiry, persisted.ExpiresTime);
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
        if (string.IsNullOrWhiteSpace(rawConnectionString) || !string.Equals(allowDatabaseCreate, "1", StringComparison.Ordinal))
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

        if (!IsApprovedLocalDataSource(builder.DataSource) || !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            Assert.Inconclusive(
                "The SQL integration fixture only accepts a local SQL/LocalDB target without AttachDbFilename.");
        }

        return builder.ConnectionString;
    }

    private static bool IsApprovedLocalDataSource(string dataSource)
    {
        var normalized = dataSource.Trim();
        return normalized.Equals(".", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("(local)", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("localhost\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase);
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName };
        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task CreateSchemaAsync(string connectionString)
    {
        const string sql = """
            CREATE TABLE dbo.hm_securityranges (
                rangeid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                rangename nvarchar(100) NOT NULL,
                rangepriorityid int NOT NULL,
                rangelowerip1 bigint NOT NULL,
                rangelowerip2 bigint NULL,
                rangeupperip1 bigint NOT NULL,
                rangeupperip2 bigint NULL,
                rangeoptions int NOT NULL,
                rangeexpires tinyint NOT NULL,
                rangeexpirestime datetime NOT NULL,
                CONSTRAINT u_hm_securityranges_rangename UNIQUE (rangename)
            );
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
}
