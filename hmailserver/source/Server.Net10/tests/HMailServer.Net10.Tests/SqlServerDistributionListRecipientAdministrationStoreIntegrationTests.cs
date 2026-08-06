using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SqlServerDistributionListRecipientAdministrationStoreIntegrationTests
{
    private const string ConnectionEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION";
    private const string AllowDatabaseCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    public async Task StoreMutations_ExhibitLegacyIdentityReadbackOwnerScopingAndStatementRollback()
    {
        var serverConnectionString = GetApprovedConnectionStringOrInconclusive();
        var databaseName = $"hmailserver_net10_dlrecipient_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(serverConnectionString, "master");
        var testConnectionString = WithDatabase(serverConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName).ConfigureAwait(false);
        try
        {
            await CreateSchemaAndSeedAsync(testConnectionString).ConfigureAwait(false);
            var store = new SqlServerDistributionListRecipientAdministrationStore(
                new SqlServerConnectionFactory(testConnectionString));

            var insertedId = await store.InsertDistributionListRecipientAsync(
                new DistributionListRecipientAdministrationSnapshot(Id: 0, ListId: 10, Address: "first@example.test"),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(insertedId > 0);

            var readBack = await store.GetRecipientsAsync(10, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(1, readBack.Count);
            Assert.AreEqual(insertedId, readBack[0].Id);
            Assert.AreEqual(10, readBack[0].ListId);
            Assert.AreEqual("first@example.test", readBack[0].Address);

            var secondId = await store.InsertDistributionListRecipientAsync(
                new DistributionListRecipientAdministrationSnapshot(Id: 0, ListId: 10, Address: "second@example.test"),
                CancellationToken.None).ConfigureAwait(false);
            Assert.AreNotEqual(insertedId, secondId);
            Assert.AreEqual(2, (await store.GetRecipientsAsync(10, CancellationToken.None).ConfigureAwait(false)).Count);

            var foreignUpdate = await store.UpdateDistributionListRecipientAsync(
                new DistributionListRecipientAdministrationSnapshot(Id: insertedId, ListId: 999, Address: "changed@example.test"),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(foreignUpdate);
            Assert.AreEqual(
                "first@example.test",
                (await store.GetRecipientsAsync(10, CancellationToken.None).ConfigureAwait(false))[0].Address);

            var ownUpdate = await store.UpdateDistributionListRecipientAsync(
                new DistributionListRecipientAdministrationSnapshot(Id: insertedId, ListId: 10, Address: "updated@example.test"),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(ownUpdate);
            var afterUpdate = await store.GetRecipientsAsync(10, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(2, afterUpdate.Count);
            var updatedRecipient = afterUpdate.Single(recipient => recipient.Id == insertedId);
            Assert.AreEqual("updated@example.test", updatedRecipient.Address);

            var foreignDelete = await store.DeleteDistributionListRecipientAsync(
                new DistributionListRecipientAdministrationSnapshot(Id: secondId, ListId: 999, Address: "second@example.test"),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(foreignDelete);
            Assert.AreEqual(2, (await store.GetRecipientsAsync(10, CancellationToken.None).ConfigureAwait(false)).Count);

            var ownDelete = await store.DeleteDistributionListRecipientAsync(
                new DistributionListRecipientAdministrationSnapshot(Id: secondId, ListId: 10, Address: "second@example.test"),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(ownDelete);
            var remaining = await store.GetRecipientsAsync(10, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(1, remaining.Count);
            Assert.AreEqual(insertedId, remaining[0].Id);

            // SqlClient truncates over-length NVarChar parameter values client-side, so the
            // natural statement-failure seam is the legacy NOT NULL address constraint.
            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.InsertDistributionListRecipientAsync(
                    new DistributionListRecipientAdministrationSnapshot(Id: 0, ListId: 10, Address: null!),
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            Assert.AreEqual(1, (await store.GetRecipientsAsync(10, CancellationToken.None).ConfigureAwait(false)).Count);

            await Assert.ThrowsExactlyAsync<SqlException>(
                () => store.UpdateDistributionListRecipientAsync(
                    new DistributionListRecipientAdministrationSnapshot(Id: insertedId, ListId: 10, Address: null!),
                    CancellationToken.None).AsTask()).ConfigureAwait(false);
            var afterFailedUpdate = await store.GetRecipientsAsync(10, CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(1, afterFailedUpdate.Count);
            Assert.AreEqual(insertedId, afterFailedUpdate[0].Id);
            Assert.AreEqual("updated@example.test", afterFailedUpdate[0].Address);
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

    private static async Task CreateSchemaAndSeedAsync(string connectionString)
    {
        const string sql = """
            CREATE TABLE dbo.hm_distributionlists (
                distributionlistid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                distributionlistdomainid int NOT NULL,
                distributionlistaddress nvarchar(255) NOT NULL,
                distributionlistenabled tinyint NOT NULL,
                distributionlistrequireauth tinyint NOT NULL,
                distributionlistrequireaddress nvarchar(255) NOT NULL,
                distributionlistmode tinyint NOT NULL
            );
            CREATE TABLE dbo.hm_distributionlistsrecipients (
                distributionlistrecipientid int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                distributionlistrecipientlistid int NOT NULL,
                distributionlistrecipientaddress nvarchar(255) NOT NULL
            );
            CREATE INDEX idx_hm_distributionlistsrecipients_distributionlistrecipientlistid
                ON dbo.hm_distributionlistsrecipients (distributionlistrecipientlistid);
            INSERT INTO dbo.hm_distributionlists
                (distributionlistdomainid, distributionlistaddress, distributionlistenabled,
                 distributionlistrequireauth, distributionlistrequireaddress, distributionlistmode)
            VALUES
                (1, N'team@example.test', 1, 0, N'', 0),
                (1, N'other@example.test', 1, 0, N'', 0);
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


