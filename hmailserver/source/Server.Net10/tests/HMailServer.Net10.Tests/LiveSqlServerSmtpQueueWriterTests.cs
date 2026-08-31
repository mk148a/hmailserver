using HMailServer.Core.Abstractions;
using HMailServer.Indexing;
using HMailServer.Protocols.Pop3;
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
    public async Task DisposableFullTextBackfillAndSearchAreUsable()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_FTS_DIAGNOSTIC"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set HMAILSERVER_NET10_LIVE_SQL_FTS_DIAGNOSTIC=1 to run the disposable SQL Full-Text diagnostic.");
        }

        var connectionString = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_CONNECTION");
        var dataRoot = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT");
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(dataRoot))
        {
            Assert.Inconclusive("HMAILSERVER_NET10_LIVE_SQL_CONNECTION and HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT are required.");
        }

        var expectedMessages = 1000;
        var expectedMessagesValue = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_FTS_EXPECTED_MESSAGES");
        if (!string.IsNullOrWhiteSpace(expectedMessagesValue)
            && (!int.TryParse(expectedMessagesValue, out expectedMessages) || expectedMessages < 1))
        {
            Assert.Fail("HMAILSERVER_NET10_LIVE_SQL_FTS_EXPECTED_MESSAGES must be a positive integer.");
        }

        if (!dataRoot.StartsWith(@"C:\hmail-perf-", StringComparison.OrdinalIgnoreCase)
            || connectionString.IndexOf("Database=hmail_perf_", StringComparison.OrdinalIgnoreCase) < 0)
        {
            Assert.Fail("The live Full-Text diagnostic accepts only hmail_perf_* SQL and C:\\hmail-perf-* Data targets.");
        }

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

        using var host = composition.Host;
        var administrationStore = host.Services.GetRequiredService<IMessageIndexingAdministrationStore>();
        var processor = host.Services.GetRequiredService<MessageSearchBackfillProcessor>();
        var searchIndex = host.Services.GetRequiredService<IMessageSearchIndex>();

        await administrationStore.SetEnabledAsync(false, CancellationToken.None);
        await administrationStore.ClearAsync(CancellationToken.None);
        await administrationStore.SetEnabledAsync(true, CancellationToken.None);

        try
        {
            var options = new MessageSearchBackfillOptions(
                LeaseOwner: "live-fts-diagnostic",
                BatchSize: 128,
                LeaseDuration: TimeSpan.FromMinutes(5),
                RetryDelay: TimeSpan.FromSeconds(1),
                MaxAttempts: 3);
            var processed = 0;
            var maxBatches = (expectedMessages + options.BatchSize - 1) / options.BatchSize + 1;
            for (var batch = 0; batch < maxBatches; batch++)
            {
                var count = await processor.RunBatchAsync(options, CancellationToken.None);
                processed += count;
                if (count == 0)
                {
                    break;
                }
            }

            Assert.AreEqual(expectedMessages, processed, "The disposable message corpus must be fully backfilled before live SEARCH acceptance.");

            var request = new ImapSearchRequest(
                AccountId: 1,
                FolderId: 1,
                MinUid: null,
                MaxUid: null,
                RequiredFlags: null,
                ForbiddenFlags: null,
                Since: null,
                Before: null,
                LargerThanBytes: null,
                SmallerThanBytes: null,
                HeaderText: null,
                BodyText: null,
                AnyText: "needle",
                ReturnUid: true);
            var matches = new List<MessageIdentity>();
            for (var attempt = 0; attempt < 60; attempt++)
            {
                matches.Clear();
                await foreach (var identity in searchIndex.SearchAsync(request, CancellationToken.None))
                {
                    matches.Add(identity);
                }

                if (matches.Count == expectedMessages)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            Assert.AreEqual(expectedMessages, matches.Count, "The indexed disposable corpus must return all needle-bearing messages.");
            Assert.AreEqual(expectedMessages, matches.Select(identity => identity.MessageId).Distinct().Count());
        }
        finally
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_FTS_KEEP"),
                    "1",
                    StringComparison.Ordinal))
            {
                await administrationStore.SetEnabledAsync(false, CancellationToken.None);
                await administrationStore.ClearAsync(CancellationToken.None);
            }
        }
    }

    [TestMethod]
    public async Task DisposablePop3AuthenticationAndMailboxLoadAreUsable()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_POP3_DIAGNOSTIC"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set HMAILSERVER_NET10_LIVE_SQL_POP3_DIAGNOSTIC=1 to run the disposable POP3 diagnostic.");
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
            Assert.Fail("The live POP3 diagnostic accepts only hmail_perf_* SQL and C:\\hmail-perf-* Data targets.");
        }

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

        using var host = composition.Host;
        var authenticator = host.Services.GetRequiredService<IImapAccountAuthenticator>();
        var authentication = await authenticator.AuthenticateAsync("test@perf.test", "test", CancellationToken.None);
        Assert.IsTrue(
            authentication.Succeeded && authentication.Account is not null,
            $"Disposable POP3 authentication failed: {authentication.FailureMessage}");

        var mailboxStore = host.Services.GetRequiredService<IPop3MailboxStore>();
        var messages = await mailboxStore.ListMessagesAsync(authentication.Account!, CancellationToken.None);
        Assert.AreEqual(1000, messages.Count);
    }

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
