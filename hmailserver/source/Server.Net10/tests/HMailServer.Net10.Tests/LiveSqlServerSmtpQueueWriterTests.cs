using HMailServer.Core.Abstractions;
using HMailServer.Service;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LiveSqlServerSmtpQueueWriterTests
{
    [TestMethod]
    public async Task DisposableHostReceiverCanPersistLocalSmtpMessage()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_SMTP_DIAGNOSTIC"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set HMAILSERVER_NET10_LIVE_SQL_SMTP_DIAGNOSTIC=1 to run against an explicitly disposable SQL/Data target.");
        }

        var connectionString = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_CONNECTION");
        var dataRoot = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT");
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(dataRoot))
        {
            Assert.Inconclusive("HMAILSERVER_NET10_LIVE_SQL_CONNECTION and HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT are required.");
        }

        if (!dataRoot.StartsWith(@"C:\hmail-perf-", StringComparison.OrdinalIgnoreCase)
            || connectionString.IndexOf("Database=hmail_perf_", StringComparison.OrdinalIgnoreCase) < 0)
        {
            Assert.Fail("The live SMTP diagnostic accepts only hmail_perf_* SQL and C:\\hmail-perf-* Data targets.");
        }

        await EnsureLegacyRuleCriteriaSchemaAsync(connectionString);

        var marker = "live-host-receiver-" + Guid.NewGuid().ToString("N");
        var mailFrom = marker + "@perf.test";
        var cleanupPaths = new List<string>();
        var composition = Host.Build(
        [
            $"--ConnectionStrings:hMailServer={connectionString}",
            $"--DataDirectory={dataRoot}",
            $"--InitializationFile={Path.Combine(dataRoot!, "hMailServer.ini")}",
            "--Smtp:Enabled=false",
            "--Imap:Enabled=false",
            "--Pop3:Enabled=false",
            "--ExternalFetch:Enabled=false",
            "--Com:LocalServerEnabled=false"
        ]);

        try
        {
            using var host = composition.Host;
            var receiver = host.Services.GetRequiredService<ISmtpMessageReceiver>();
            var result = await receiver.ReceiveAsync(
                new SmtpReceiveRequest(
                    HeloHost: "client.example",
                    IsExtendedSmtp: true,
                    MailFrom: mailFrom,
                    Recipients:
                    [new SmtpResolvedRecipient("test@perf.test", "test@perf.test", 1, true)],
                    DeclaredSize: null,
                    MessageData: "Subject: host receiver diagnostic\r\n\r\nBody\r\n"u8.ToArray(),
                    ReceivedUtc: DateTimeOffset.UtcNow,
                    ClientIPAddress: "127.0.0.1",
                    ClientPort: 25,
                    SessionId: 991,
                    AuthenticatedUsername: string.Empty,
                    IsAuthenticated: false,
                    IsEncryptedConnection: false),
                CancellationToken.None);

            if (!result.Accepted)
            {
                Assert.Fail($"Host SMTP receiver rejected the disposable message: {result.FailureResponse}");
            }
        }
        finally
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using (var select = new SqlCommand(
                       "SELECT messagefilename FROM hm_messages WHERE messagefrom = @MailFrom;",
                       connection))
            {
                select.Parameters.AddWithValue("@MailFrom", mailFrom);
                await using var reader = await select.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    cleanupPaths.Add(reader.GetString(0));
                }
            }

            await using var cleanup = new SqlCommand(
                "DELETE r FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid = r.recipientmessageid WHERE m.messagefrom = @MailFrom; DELETE FROM hm_messages WHERE messagefrom = @MailFrom;",
                connection);
            cleanup.Parameters.AddWithValue("@MailFrom", mailFrom);
            await cleanup.ExecuteNonQueryAsync();

            foreach (var path in cleanupPaths)
            {
                DeleteStoredMessageFile(dataRoot, path);
            }
        }
    }

    private static async Task EnsureLegacyRuleCriteriaSchemaAsync(string connectionString)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.hm_rule_criterias', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.hm_rule_criterias
                (
                    criteriaid int IDENTITY(1,1) NOT NULL,
                    criteriaruleid int NOT NULL,
                    criteriausepredefined tinyint NOT NULL,
                    criteriapredefinedfield tinyint NOT NULL,
                    criteriaheadername nvarchar(255) NOT NULL,
                    criteriamatchtype tinyint NOT NULL,
                    criteriamatchvalue nvarchar(255) NOT NULL
                );
                ALTER TABLE dbo.hm_rule_criterias
                    ADD CONSTRAINT hm_rule_criterias_pk PRIMARY KEY NONCLUSTERED (criteriaid);
                CREATE CLUSTERED INDEX idx_hm_rule_criterias
                    ON dbo.hm_rule_criterias (criteriaruleid);
            END;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [TestMethod]
    public async Task DisposableBenchmarkAcceptanceRowsCanBeCleaned()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_SMTP_CLEANUP"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set HMAILSERVER_NET10_LIVE_SQL_SMTP_CLEANUP=1 to clean only the disposable SMTP benchmark rows.");
        }

        var connectionString = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_CONNECTION");
        var dataRoot = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT");
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(dataRoot))
        {
            Assert.Inconclusive("HMAILSERVER_NET10_LIVE_SQL_CONNECTION and HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT are required.");
        }

        if (!dataRoot.StartsWith(@"C:\hmail-perf-", StringComparison.OrdinalIgnoreCase)
            || connectionString.IndexOf("Database=hmail_perf_", StringComparison.OrdinalIgnoreCase) < 0)
        {
            Assert.Fail("The live SMTP cleanup accepts only hmail_perf_* SQL and C:\\hmail-perf-* Data targets.");
        }

        var cleanupPaths = new List<string>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var select = new SqlCommand(
                   "SELECT messagefilename FROM hm_messages WHERE messageid > 1000 AND messagefrom LIKE N'sender%@perf.test';",
                   connection))
        {
            await using var reader = await select.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                cleanupPaths.Add(reader.GetString(0));
            }
        }

        await using (var cleanup = new SqlCommand(
                   "DELETE r FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid = r.recipientmessageid WHERE m.messageid > 1000 AND m.messagefrom LIKE N'sender%@perf.test'; DELETE FROM hm_messages WHERE messageid > 1000 AND messagefrom LIKE N'sender%@perf.test';",
                   connection))
        {
            await cleanup.ExecuteNonQueryAsync();
        }

        foreach (var path in cleanupPaths)
        {
            DeleteStoredMessageFile(dataRoot, path);
        }

        await using var remaining = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM hm_messages WHERE messageid > 1000 AND messagefrom LIKE N'sender%@perf.test';",
            connection);
        Assert.AreEqual(0L, Convert.ToInt64(await remaining.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task DisposableQueueWriterCanInsertAndCleanup()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_SMTP_DIAGNOSTIC"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set HMAILSERVER_NET10_LIVE_SQL_SMTP_DIAGNOSTIC=1 to run against an explicitly disposable SQL/Data target.");
        }

        var connectionString = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_CONNECTION");
        var dataRoot = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT");
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(dataRoot))
        {
            Assert.Inconclusive("HMAILSERVER_NET10_LIVE_SQL_CONNECTION and HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT are required.");
        }

        if (!dataRoot.StartsWith(@"C:\hmail-perf-", StringComparison.OrdinalIgnoreCase)
            || connectionString.IndexOf("Database=hmail_perf_", StringComparison.OrdinalIgnoreCase) < 0)
        {
            Assert.Fail("The live SMTP diagnostic accepts only hmail_perf_* SQL and C:\\hmail-perf-* Data targets.");
        }

        var marker = "live-smtp-diagnostic-" + Guid.NewGuid().ToString("N");
        var mailFrom = marker + "@perf.test";
        Exception? failure = null;
        var cleanupPaths = new List<string>();
        try
        {
            var writer = new SqlServerSmtpQueueWriter(
                new SqlServerConnectionFactory(connectionString),
                new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(dataRoot)));

            try
            {
                await writer.EnqueueAsync(
                    new SmtpQueueWriteRequest(
                        mailFrom,
                        [new SmtpResolvedRecipient("test@perf.test", "test@perf.test", 1, true)],
                        "Subject: live diagnostic\r\n\r\nBody\r\n"u8.ToArray(),
                        DateTimeOffset.UtcNow),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using (var select = new SqlCommand(
                       "SELECT messageid, messagefilename FROM hm_messages WHERE messagefrom = @MailFrom;",
                       connection))
            {
                select.Parameters.AddWithValue("@MailFrom", mailFrom);
                await using var reader = await select.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    cleanupPaths.Add(reader.GetString(1));
                }
            }

            await using (var cleanup = new SqlCommand(
                       "DELETE r FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid = r.recipientmessageid WHERE m.messagefrom = @MailFrom; DELETE FROM hm_messages WHERE messagefrom = @MailFrom;",
                       connection))
            {
                cleanup.Parameters.AddWithValue("@MailFrom", mailFrom);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            failure ??= ex;
        }
        finally
        {
            foreach (var path in cleanupPaths)
            {
                try
                {
                    DeleteStoredMessageFile(dataRoot, path);
                }
                catch
                {
                    // The test must preserve the original provider failure.
                }
            }
        }

        if (failure is not null)
        {
            Assert.Fail($"Disposable SMTP queue writer failed: {failure}");
        }
    }

    private static void DeleteStoredMessageFile(string dataRoot, string storedPath)
    {
        var path = Path.IsPathRooted(storedPath)
            ? storedPath
            : Path.Combine(dataRoot, storedPath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
