using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using HMailServer.Service;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LiveSqlServerDeliveryQueueTests
{
    [TestMethod]
    public async Task DisposableDeliveryQueueLocalDeliveryAndRetryAreUsable()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DELIVERY_DIAGNOSTIC"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set HMAILSERVER_NET10_LIVE_SQL_DELIVERY_DIAGNOSTIC=1 to run the disposable delivery queue diagnostic.");
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
            Assert.Fail("The live delivery diagnostic accepts only hmail_perf_* SQL and C:\\hmail-perf-* Data targets.");
        }

        var localCount = ReadBoundedInt("HMAILSERVER_NET10_LIVE_SQL_DELIVERY_COUNT", 50, 1, 500);
        var localMarker = "live-delivery-local-" + Guid.NewGuid().ToString("N");
        var retryMarker = "live-delivery-retry-" + Guid.NewGuid().ToString("N");
        var localFrom = localMarker + "@perf.test";
        var retryFrom = retryMarker + "@perf.test";
        var resolver = new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(dataRoot));
        var factory = new SqlServerConnectionFactory(connectionString);
        var queueWriter = new SqlServerSmtpQueueWriter(factory, resolver);
        var localSamples = new List<double>(localCount);
        var localStopwatch = Stopwatch.StartNew();
        var retryEvidence = new RetryEvidence();

        var composition = Host.Build(
        [
            $"--ConnectionStrings:hMailServer={connectionString}",
            $"--DataDirectory={dataRoot}",
            $"--InitializationFile={Path.Combine(dataRoot, "hMailServer.ini")}",
            "--Smtp:Enabled=false",
            "--Imap:Enabled=false",
            "--Pop3:Enabled=false",
            "--ExternalFetch:Enabled=false",
            "--Com:LocalServerEnabled=false"
        ]);

        try
        {
            using var host = composition.Host;
            var statusObserver = new RecordingStatusObserver();
            var processor = new DeliveryQueueProcessor(
                host.Services.GetRequiredService<IDeliveryQueueLeaseStore>(),
                host.Services.GetRequiredService<IDeliveryQueueMessageStore>(),
                host.Services.GetRequiredService<IDeliveryTargetResolver>(),
                host.Services.GetRequiredService<IDeliveryTargetDispatcher>(),
                host.Services.GetRequiredService<IDeliveryQueueRecipientStore>(),
                host.Services.GetRequiredService<IDeliveryBounceStore>(),
                messageContentStore: new DeliveryMessageContentSource(resolver),
                statusObserver: statusObserver);
            var localOptions = new DeliveryQueueProcessorOptions(
                LeaseOwner: "live-delivery-local-" + Guid.NewGuid().ToString("N"),
                BatchSize: 1,
                LeaseDuration: TimeSpan.FromMinutes(2),
                RetryDelay: TimeSpan.FromSeconds(1),
                MaxRetries: 4,
                MaxRetryDelay: TimeSpan.FromMinutes(1));

            var messageData = Encoding.ASCII.GetBytes(
                "From: sender@perf.test\r\nTo: test@perf.test\r\nSubject: delivery queue diagnostic\r\n\r\nqueue body\r\n");
            for (var index = 0; index < localCount; index++)
            {
                await queueWriter.EnqueueAsync(
                    new SmtpQueueWriteRequest(
                        localFrom,
                        [new SmtpResolvedRecipient("test@perf.test", "test@perf.test", 1, true)],
                        messageData,
                        DateTimeOffset.UtcNow),
                    CancellationToken.None);
            }

            var processed = 0;
            for (var batch = 0; batch <= localCount; batch++)
            {
                var sample = Stopwatch.StartNew();
                var batchProcessed = await processor.RunBatchAsync(localOptions, CancellationToken.None);
                sample.Stop();
                if (batchProcessed == 0)
                {
                    break;
                }

                Assert.AreEqual(1, batchProcessed, "The bounded local-delivery benchmark must use one message per measured batch.");
                processed += batchProcessed;
                localSamples.Add(sample.Elapsed.TotalMilliseconds);
            }

            localStopwatch.Stop();
            Assert.AreEqual(localCount, processed);
            var localState = await ReadMessageStateAsync(connectionString, localFrom);
            Assert.AreEqual(0, localState.QueuedCount, string.Join(" | ", statusObserver.Events.Select(static item => item.Kind + ":" + item.Description)));
            Assert.AreEqual(localCount, localState.DeliveredCount, string.Join(" | ", statusObserver.Events.Select(static item => item.Kind + ":" + item.Description)));
            Assert.AreEqual(0L, localState.RecipientCount, "Local delivery removes the queue recipient rows after the Inbox copies are committed.");

            await queueWriter.EnqueueAsync(
                new SmtpQueueWriteRequest(
                    retryFrom,
                    [new SmtpResolvedRecipient("unreachable@retry.test", "unreachable@retry.test", 0, false)],
                    messageData,
                    DateTimeOffset.UtcNow),
                CancellationToken.None);

            var retryProcessor = new DeliveryQueueProcessor(
                new SqlServerDeliveryQueueLeaseStore(factory),
                new SqlServerDeliveryQueueMessageStore(factory),
                new FixedRetryTargetResolver(),
                new AlwaysTransientDispatcher(),
                new SqlServerDeliveryQueueRecipientStore(factory),
                new NoopBounceStore(),
                messageContentStore: new DeliveryMessageContentSource(resolver));
            var retryOptions = new DeliveryQueueProcessorOptions(
                LeaseOwner: "live-delivery-retry-" + Guid.NewGuid().ToString("N"),
                BatchSize: 1,
                LeaseDuration: TimeSpan.FromMinutes(2),
                RetryDelay: TimeSpan.FromSeconds(30),
                MaxRetries: 4,
                MaxRetryDelay: TimeSpan.FromMinutes(1));

            Assert.AreEqual(1, await retryProcessor.RunBatchAsync(retryOptions, CancellationToken.None));
            retryEvidence = await ReadRetryEvidenceAsync(connectionString, retryFrom);
            Assert.AreEqual(1, retryEvidence.QueuedCount);
            Assert.AreEqual(0, retryEvidence.Locked);
            Assert.IsTrue(retryEvidence.LeaseOwnerIsNull);
            Assert.AreEqual(1, retryEvidence.RetryCount);
            Assert.IsTrue(retryEvidence.NextTryUtc > DateTime.UtcNow.AddSeconds(10));
            Assert.AreEqual(1, retryEvidence.RecipientCount);

            await WriteReportIfRequestedAsync(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DELIVERY_OUTPUT"),
                connectionString,
                dataRoot,
                localCount,
                localSamples,
                localStopwatch.Elapsed,
                retryEvidence);
        }
        finally
        {
            await CleanupMessagesAsync(connectionString, dataRoot, localFrom, retryFrom, resolver);
        }
    }

    [TestMethod]
    public async Task DisposableDeliveryQueueRealTcp451RetainsRetryState()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DELIVERY_DIAGNOSTIC"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set HMAILSERVER_NET10_LIVE_SQL_DELIVERY_DIAGNOSTIC=1 to run the disposable TCP 451 retry diagnostic.");
        }

        var connectionString = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_CONNECTION");
        var dataRoot = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT");
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(dataRoot))
        {
            Assert.Inconclusive("HMAILSERVER_NET10_LIVE_SQL_CONNECTION and HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT are required.");
        }

        var isolatedDataRoot = ValidateDisposableDataRoot(dataRoot);
        var parsedConnectionString = new SqlConnectionStringBuilder(connectionString);
        if (!IsDisposableDatabaseName(parsedConnectionString.InitialCatalog))
        {
            Assert.Fail("The live delivery diagnostic accepts only a parsed hmail_perf_* database and a canonical C:\\hmail-perf-* Data root.");
        }

        var marker = "live-delivery-tcp-451-" + Guid.NewGuid().ToString("N");
        var fromAddress = marker + "@perf.test";
        var pathResolver = new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(isolatedDataRoot));
        var connectionFactory = new SqlServerConnectionFactory(connectionString);
        var queueWriter = new SqlServerSmtpQueueWriter(connectionFactory, pathResolver);
        var messageData = Encoding.ASCII.GetBytes(
            "From: " + fromAddress + "\r\nTo: unreachable@retry.test\r\nSubject: tcp 451 retry\r\n\r\nretry body\r\n");

        try
        {
            await queueWriter.EnqueueAsync(
                new SmtpQueueWriteRequest(
                    fromAddress,
                    [new SmtpResolvedRecipient("unreachable@retry.test", "unreachable@retry.test", 0, false)],
                    messageData,
                    DateTimeOffset.UtcNow),
                CancellationToken.None);

            await using var sink = await TransientSmtp451Sink.StartAsync();
            var endpoint = new RemoteSmtpEndpoint(
                "loopback-retry.invalid",
                sink.Port,
                RemoteSmtpConnectionSecurity.None,
                ConnectionAddress: IPAddress.Loopback.ToString(),
                EnforceLocalEndpointGuard: false);
            var dispatcher = new RemoteDeliveryTargetDispatcher(
                new FixedEndpointResolver(endpoint),
                new DeliveryMessageContentSource(pathResolver),
                new SmtpRemoteDeliveryClient(new TcpRemoteSmtpTransportFactory()),
                new RemoteDeliveryOptions("mail.local.test", TimeSpan.FromSeconds(30)));
            var statusObserver = new RecordingStatusObserver();
            var processor = new DeliveryQueueProcessor(
                new SqlServerDeliveryQueueLeaseStore(connectionFactory),
                new SqlServerDeliveryQueueMessageStore(connectionFactory),
                new FixedRetryTargetResolver(),
                dispatcher,
                new SqlServerDeliveryQueueRecipientStore(connectionFactory),
                new NoopBounceStore(),
                statusObserver: statusObserver);
            var options = new DeliveryQueueProcessorOptions(
                LeaseOwner: "live-delivery-tcp-451-" + Guid.NewGuid().ToString("N"),
                BatchSize: 1,
                LeaseDuration: TimeSpan.FromMinutes(2),
                RetryDelay: TimeSpan.FromSeconds(30),
                MaxRetries: 4,
                MaxRetryDelay: TimeSpan.FromMinutes(1));

            Assert.AreEqual(1, await processor.RunBatchAsync(options, CancellationToken.None));
            await sink.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            var evidence = await ReadRetryEvidenceAsync(connectionString, fromAddress);

            Assert.IsTrue(sink.SawEhloOrHelo);
            Assert.IsTrue(sink.SawMailFrom);
            Assert.IsTrue(sink.SawRecipient);
            Assert.IsTrue(sink.Saw451ResponseSent);
            Assert.IsFalse(sink.SawData, "A 451 RCPT reply must stop before DATA.");
            Assert.AreEqual(1, evidence.QueuedCount);
            Assert.AreEqual(1, evidence.MessageType);
            Assert.AreEqual(0, evidence.Locked);
            Assert.IsTrue(evidence.LeaseOwnerIsNull);
            Assert.AreEqual(1, evidence.RetryCount);
            Assert.IsTrue(evidence.NextTryUtc > DateTime.UtcNow.AddSeconds(10));
            Assert.AreEqual(1, evidence.RecipientCount);
            CollectionAssert.Contains(
                statusObserver.Events.Select(static item => item.Kind).ToList(),
                DeliveryQueueStatusEventKind.TargetDeliveryDeferred);
            CollectionAssert.Contains(
                statusObserver.Events.Select(static item => item.Kind).ToList(),
                DeliveryQueueStatusEventKind.MessageDeferred);

            await WriteTcp451ReportIfRequestedAsync(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_TCP451_OUTPUT"),
                parsedConnectionString.InitialCatalog,
                isolatedDataRoot,
                evidence,
                sink,
                statusObserver);
        }
        finally
        {
            await CleanupMessagesAsync(connectionString, isolatedDataRoot, string.Empty, fromAddress, pathResolver);
        }
    }

    [TestMethod]
    public async Task DisposableDeliveryQueueRealTcp451Then250CompletesMessage()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DELIVERY_DIAGNOSTIC"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set HMAILSERVER_NET10_LIVE_SQL_DELIVERY_DIAGNOSTIC=1 to run the disposable TCP 451 recovery diagnostic.");
        }

        var connectionString = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_CONNECTION");
        var dataRoot = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT");
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(dataRoot))
        {
            Assert.Inconclusive("HMAILSERVER_NET10_LIVE_SQL_CONNECTION and HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT are required.");
        }

        var isolatedDataRoot = ValidateDisposableDataRoot(dataRoot);
        var parsedConnectionString = new SqlConnectionStringBuilder(connectionString);
        if (!IsDisposableDatabaseName(parsedConnectionString.InitialCatalog))
        {
            Assert.Fail("The live delivery diagnostic accepts only a parsed hmail_perf_* database and a canonical C:\\hmail-perf-* Data root.");
        }

        var marker = "live-delivery-tcp-451-recovery-" + Guid.NewGuid().ToString("N");
        var fromAddress = marker + "@perf.test";
        var pathResolver = new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(isolatedDataRoot));
        var connectionFactory = new SqlServerConnectionFactory(connectionString);
        var queueWriter = new SqlServerSmtpQueueWriter(connectionFactory, pathResolver);
        var messageData = Encoding.ASCII.GetBytes(
            "From: " + fromAddress + "\r\nTo: recovered@retry.test\r\nSubject: tcp 451 recovery\r\n\r\nrecovery body\r\n");

        try
        {
            await queueWriter.EnqueueAsync(
                new SmtpQueueWriteRequest(
                    fromAddress,
                    [new SmtpResolvedRecipient("recovered@retry.test", "recovered@retry.test", 0, false)],
                    messageData,
                    DateTimeOffset.UtcNow),
                CancellationToken.None);

            var storedFileName = await ReadMessageFileNameAsync(connectionString, fromAddress);
            var messageFilePath = pathResolver.Resolve(storedFileName, accountId: 0, folderId: 0, accountAddress: null);
            if (messageFilePath is null)
            {
                Assert.Fail("The queued message filename did not resolve under the disposable Data root.");
            }

            Assert.IsTrue(File.Exists(messageFilePath));

            await using var sink = await StatefulSmtpRecoverySink.StartAsync();
            var endpoint = new RemoteSmtpEndpoint(
                "loopback-recovery.invalid",
                sink.Port,
                RemoteSmtpConnectionSecurity.None,
                ConnectionAddress: IPAddress.Loopback.ToString(),
                EnforceLocalEndpointGuard: false);
            var dispatcher = new RemoteDeliveryTargetDispatcher(
                new FixedEndpointResolver(endpoint),
                new DeliveryMessageContentSource(pathResolver),
                new SmtpRemoteDeliveryClient(new TcpRemoteSmtpTransportFactory()),
                new RemoteDeliveryOptions("mail.local.test", TimeSpan.Zero));
            var statusObserver = new RecordingStatusObserver();
            var processor = new DeliveryQueueProcessor(
                new SqlServerDeliveryQueueLeaseStore(connectionFactory),
                new SqlServerDeliveryQueueMessageStore(connectionFactory),
                new FixedRetryTargetResolver(),
                dispatcher,
                new SqlServerDeliveryQueueRecipientStore(connectionFactory),
                new NoopBounceStore(),
                messageContentStore: new DeliveryMessageContentSource(pathResolver),
                statusObserver: statusObserver);
            var options = new DeliveryQueueProcessorOptions(
                LeaseOwner: "live-delivery-tcp-451-recovery-" + Guid.NewGuid().ToString("N"),
                BatchSize: 1,
                LeaseDuration: TimeSpan.FromMinutes(2),
                RetryDelay: TimeSpan.Zero,
                MaxRetries: 4,
                MaxRetryDelay: TimeSpan.FromMinutes(1));

            Assert.AreEqual(1, await processor.RunBatchAsync(options, CancellationToken.None));
            await sink.FirstAttemptCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            var retryEvidence = await ReadRetryEvidenceAsync(connectionString, fromAddress);
            Assert.IsTrue(sink.Saw451ResponseSent);
            Assert.IsFalse(sink.SawDataBeforeRecovery);
            Assert.AreEqual(1, retryEvidence.QueuedCount);
            Assert.AreEqual(1, retryEvidence.MessageType);
            Assert.AreEqual(0, retryEvidence.Locked);
            Assert.IsTrue(retryEvidence.LeaseOwnerIsNull);
            Assert.AreEqual(1, retryEvidence.RetryCount);
            Assert.AreEqual(1, retryEvidence.RecipientCount);

            var recovered = 0;
            for (var attempt = 0; attempt < 10 && recovered == 0; attempt++)
            {
                await Task.Delay(100);
                recovered = await processor.RunBatchAsync(options, CancellationToken.None);
            }

            Assert.AreEqual(1, recovered);
            await sink.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            var finalState = await ReadMessageStateAsync(connectionString, fromAddress);
            Assert.AreEqual(0, finalState.QueuedCount);
            Assert.AreEqual(0, finalState.DeliveredCount);
            Assert.AreEqual(0, finalState.RecipientCount);
            Assert.IsTrue(sink.SawRecoveryResponse);
            Assert.IsTrue(sink.SawDataAfterRecovery);
            Assert.IsTrue(statusObserver.Events.Any(static item => item.Kind == DeliveryQueueStatusEventKind.TargetDeliveryDeferred));
            Assert.IsTrue(statusObserver.Events.Any(static item => item.Kind == DeliveryQueueStatusEventKind.TargetDeliverySucceeded));
            Assert.IsFalse(File.Exists(messageFilePath));
            await WriteTcp451RecoveryReportIfRequestedAsync(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DELIVERY_RECOVERY_REPORT"),
                parsedConnectionString.InitialCatalog,
                isolatedDataRoot,
                retryEvidence,
                finalState,
                sink,
                statusObserver);
        }
        finally
        {
            await CleanupMessagesAsync(connectionString, isolatedDataRoot, string.Empty, fromAddress, pathResolver);
        }
    }

    private static async Task<string> ReadMessageFileNameAsync(string connectionString, string marker)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT TOP (1) messagefilename FROM hm_messages WHERE messagefrom = @MessageFrom;", connection);
        command.Parameters.AddWithValue("@MessageFrom", marker);
        return (await command.ExecuteScalarAsync()) as string ?? string.Empty;
    }

    private static string ValidateDisposableDataRoot(string dataRoot)
    {
        var fullPath = Path.GetFullPath(dataRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!fullPath.StartsWith(@"C:\hmail-perf-", StringComparison.OrdinalIgnoreCase))
        {
            throw new AssertFailedException("The live delivery diagnostic accepts only C:\\hmail-perf-* Data roots.");
        }

        for (var current = new DirectoryInfo(fullPath); current is not null; current = current.Parent)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new AssertFailedException("The live delivery diagnostic rejects reparse-point Data roots.");
            }

            if (string.Equals(current.FullName, current.Root.FullName.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return fullPath;
    }

    private static bool IsDisposableDatabaseName(string databaseName) =>
        databaseName.Length > "hmail_perf_".Length
        && databaseName.StartsWith("hmail_perf_", StringComparison.OrdinalIgnoreCase)
        && databaseName.Skip("hmail_perf_".Length).All(static character => char.IsLetterOrDigit(character) || character == '_');

    private static int ReadBoundedInt(string name, int defaultValue, int minimum, int maximum)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : defaultValue;
    }

    private static async Task<MessageState> ReadMessageStateAsync(string connectionString, string marker)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT messagetype, COUNT_BIG(*) FROM hm_messages WHERE messagefrom = @MessageFrom GROUP BY messagetype; SELECT COUNT_BIG(*) FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid = r.recipientmessageid WHERE m.messagefrom = @MessageFrom;",
            connection);
        command.Parameters.AddWithValue("@MessageFrom", marker);
        var queued = 0L;
        var delivered = 0L;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var type = reader.GetByte(0);
            var count = reader.GetInt64(1);
            if (type == 1) queued = count;
            if (type == 2) delivered = count;
        }

        await reader.NextResultAsync();
        var recipients = Convert.ToInt64(await reader.ReadAsync() ? reader.GetValue(0) : 0, System.Globalization.CultureInfo.InvariantCulture);
        return new MessageState(queued, delivered, recipients);
    }

    private static async Task<RetryEvidence> ReadRetryEvidenceAsync(string connectionString, string marker)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT_BIG(*), MAX(CAST(messagetype AS int)), MAX(CAST(messagelocked AS int)), MAX(messagecurnooftries), MAX(messagenexttrytime), MAX(CASE WHEN messageleaseowner IS NULL THEN 1 ELSE 0 END) FROM hm_messages WHERE messagefrom = @MessageFrom; SELECT COUNT_BIG(*) FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid = r.recipientmessageid WHERE m.messagefrom = @MessageFrom;",
            connection);
        command.Parameters.AddWithValue("@MessageFrom", marker);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var evidence = new RetryEvidence(
            Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(2), System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture),
            DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc),
            Convert.ToInt32(reader.GetValue(5), System.Globalization.CultureInfo.InvariantCulture) == 1);
        await reader.NextResultAsync();
        await reader.ReadAsync();
        return evidence with { RecipientCount = Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) };
    }

    private static async Task CleanupMessagesAsync(
        string connectionString,
        string dataRoot,
        string localFrom,
        string retryFrom,
        MessageFilePathResolver resolver)
    {
        var files = new List<StoredMessage>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var select = new SqlCommand(
                   "SELECT messageid, messagefilename, messageaccountid, messagefolderid FROM hm_messages WHERE messagefrom IN (@LocalFrom, @RetryFrom);",
                   connection))
        {
            select.Parameters.AddWithValue("@LocalFrom", localFrom);
            select.Parameters.AddWithValue("@RetryFrom", retryFrom);
            await using var reader = await select.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                files.Add(new StoredMessage(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3)));
            }
        }

        await using (var cleanup = new SqlCommand(
                   "DELETE q FROM hm_message_search_queue q INNER JOIN hm_messages m ON m.messageid = q.messageid WHERE m.messagefrom IN (@LocalFrom, @RetryFrom); DELETE d FROM hm_message_search_documents d INNER JOIN hm_messages m ON m.messageid = d.messageid WHERE m.messagefrom IN (@LocalFrom, @RetryFrom); DELETE r FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid = r.recipientmessageid WHERE m.messagefrom IN (@LocalFrom, @RetryFrom); DELETE FROM hm_messages WHERE messagefrom IN (@LocalFrom, @RetryFrom);",
                   connection))
        {
            cleanup.Parameters.AddWithValue("@LocalFrom", localFrom);
            cleanup.Parameters.AddWithValue("@RetryFrom", retryFrom);
            await cleanup.ExecuteNonQueryAsync();
        }

        foreach (var file in files)
        {
            var accountAddress = file.AccountId == 1 ? "test@perf.test" : null;
            var path = resolver.Resolve(file.FileName, file.AccountId, file.FolderId, accountAddress);
            if (path is not null && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        await using (var verify = new SqlCommand(
                   "SELECT COUNT_BIG(*) FROM hm_messages WHERE messagefrom IN (@LocalFrom, @RetryFrom); SELECT COUNT_BIG(*) FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid = r.recipientmessageid WHERE m.messagefrom IN (@LocalFrom, @RetryFrom);",
                   connection))
        {
            verify.Parameters.AddWithValue("@LocalFrom", localFrom);
            verify.Parameters.AddWithValue("@RetryFrom", retryFrom);
            await using var reader = await verify.ExecuteReaderAsync();
            await reader.ReadAsync();
            if (Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) != 0)
            {
                throw new InvalidOperationException("Disposable delivery cleanup left message rows behind.");
            }

            await reader.NextResultAsync();
            await reader.ReadAsync();
            if (Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) != 0)
            {
                throw new InvalidOperationException("Disposable delivery cleanup left recipient rows behind.");
            }
        }

        foreach (var file in files)
        {
            var accountAddress = file.AccountId == 1 ? "test@perf.test" : null;
            var path = resolver.Resolve(file.FileName, file.AccountId, file.FolderId, accountAddress);
            if (path is not null && File.Exists(path))
            {
                throw new IOException("Disposable delivery cleanup left a message file behind: " + path);
            }
        }
    }

    private static async Task WriteReportIfRequestedAsync(
        string? outputPath,
        string connectionString,
        string dataRoot,
        int localCount,
        IReadOnlyList<double> samples,
        TimeSpan totalDuration,
        RetryEvidence retryEvidence)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The delivery benchmark output path must include a directory.");
        }

        Directory.CreateDirectory(directory);
        var ordered = samples.OrderBy(static value => value).ToArray();
        var report = new DeliveryBenchmarkReport(
            "net10-live-delivery-queue-v1",
            DateTimeOffset.UtcNow,
            connectionString.Split(';').FirstOrDefault(static value => value.StartsWith("Database=", StringComparison.OrdinalIgnoreCase)) ?? "Database=redacted",
            dataRoot,
            localCount,
            samples.Count,
            totalDuration.TotalMilliseconds,
            localCount / Math.Max(totalDuration.TotalSeconds, 0.001),
            Percentile(ordered, 50),
            Percentile(ordered, 95),
            Percentile(ordered, 99),
            retryEvidence);
        await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        await File.WriteAllTextAsync(Path.ChangeExtension(fullPath, ".csv"), "scenario,samples,p50_ms,p95_ms,p99_ms,total_ms,throughput_messages_per_second\nlocal-delivery," + samples.Count + "," + report.P50Milliseconds + "," + report.P95Milliseconds + "," + report.P99Milliseconds + "," + report.TotalMilliseconds + "," + report.ThroughputMessagesPerSecond + "\n");
        var markdown = $"# Net10 live delivery queue\n\n- Local messages: `{localCount}`\n- Samples: `{samples.Count}`\n- p50/p95/p99: `{report.P50Milliseconds}` / `{report.P95Milliseconds}` / `{report.P99Milliseconds}` ms\n- Throughput: `{report.ThroughputMessagesPerSecond}` messages/s\n- Retry evidence: one SQL queue row retained, unlocked, retry count 1, future next-try timestamp, recipient retained\n\nJSON: `{fullPath}`\n";
        await File.WriteAllTextAsync(Path.ChangeExtension(fullPath, ".md"), markdown);
    }

    private static async Task WriteTcp451ReportIfRequestedAsync(
        string? outputPath,
        string database,
        string dataRoot,
        RetryEvidence evidence,
        TransientSmtp451Sink sink,
        RecordingStatusObserver statusObserver)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The TCP 451 report path must include a directory.");
        }

        Directory.CreateDirectory(directory);
        var report = new
        {
            schema = "net10-live-tcp-451-retry-v1",
            status = "PASS",
            generatedUtc = DateTimeOffset.UtcNow,
            database,
            dataRoot,
            smtpReply = 451,
            sawEhloOrHelo = sink.SawEhloOrHelo,
            sawMailFrom = sink.SawMailFrom,
            sawRecipient = sink.SawRecipient,
            saw451ResponseSent = sink.Saw451ResponseSent,
            sawData = sink.SawData,
            messageType = evidence.MessageType,
            queuedCount = evidence.QueuedCount,
            locked = evidence.Locked,
            leaseOwnerIsNull = evidence.LeaseOwnerIsNull,
            retryCount = evidence.RetryCount,
            nextTryUtc = evidence.NextTryUtc,
            recipientCount = evidence.RecipientCount,
            deferredEvents = statusObserver.Events.Count(static item => item.Kind is DeliveryQueueStatusEventKind.TargetDeliveryDeferred or DeliveryQueueStatusEventKind.MessageDeferred)
        };
        await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        await File.WriteAllTextAsync(
            Path.ChangeExtension(fullPath, ".csv"),
            "status,smtp_reply,message_type,queued_count,locked,lease_owner_is_null,retry_count,recipient_count,saw_data,deferred_events\nPASS,451," + evidence.MessageType + "," + evidence.QueuedCount + "," + evidence.Locked + "," + evidence.LeaseOwnerIsNull + "," + evidence.RetryCount + "," + evidence.RecipientCount + "," + sink.SawData + "," + report.deferredEvents + "\n");
        await File.WriteAllTextAsync(
            Path.ChangeExtension(fullPath, ".md"),
            $"# Net10 TCP 451 retry state\n\n- Result: `PASS`\n- SMTP reply: `451`\n- SQL database: `{database}`\n- Data root: `{dataRoot}`\n- Queue state: `messagetype={evidence.MessageType}`, `queued={evidence.QueuedCount}`, `locked={evidence.Locked}`, `leaseOwnerIsNull={evidence.LeaseOwnerIsNull}`\n- Retry state: `retryCount={evidence.RetryCount}`, `recipientCount={evidence.RecipientCount}`, `nextTryUtc={evidence.NextTryUtc:O}`\n- Protocol guard: EHLO/HELO, MAIL FROM, and RCPT observed; `451` sent; DATA observed: `{sink.SawData}`\n- Deferred status events: `{report.deferredEvents}`\n\nThis is Net10 component-level disposable evidence. It is not paired C++ evidence and does not clear the production performance gate.\n\nJSON: `{fullPath}`\n");
    }

    private static async Task WriteTcp451RecoveryReportIfRequestedAsync(
        string? outputPath,
        string database,
        string dataRoot,
        RetryEvidence initialEvidence,
        MessageState finalState,
        StatefulSmtpRecoverySink sink,
        RecordingStatusObserver statusObserver)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The TCP 451 recovery report path must include a directory.");
        }

        Directory.CreateDirectory(directory);
        var deferredEvents = statusObserver.Events.Count(static item => item.Kind is DeliveryQueueStatusEventKind.TargetDeliveryDeferred or DeliveryQueueStatusEventKind.MessageDeferred);
        var report = new
        {
            schema = "net10-live-tcp-451-recovery-v1",
            status = "PASS",
            generatedUtc = DateTimeOffset.UtcNow,
            database,
            dataRoot,
            firstAttempt = new
            {
                smtpReply = 451,
                initialEvidence.MessageType,
                queuedCount = initialEvidence.QueuedCount,
                initialEvidence.Locked,
                initialEvidence.LeaseOwnerIsNull,
                initialEvidence.RetryCount,
                initialEvidence.NextTryUtc,
                initialEvidence.RecipientCount,
                saw451ResponseSent = sink.Saw451ResponseSent,
                sawData = sink.SawDataBeforeRecovery
            },
            recoveryAttempt = new
            {
                smtpReply = 250,
                sawRecoveryResponse = sink.SawRecoveryResponse,
                sawData = sink.SawDataAfterRecovery
            },
            finalState,
            deferredEvents,
            succeededEvents = statusObserver.Events.Count(static item => item.Kind == DeliveryQueueStatusEventKind.TargetDeliverySucceeded),
            messageFileAbsent = true
        };
        await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        await File.WriteAllTextAsync(
            Path.ChangeExtension(fullPath, ".csv"),
            "status,first_smtp_reply,recovery_smtp_reply,initial_queued_count,initial_retry_count,final_queued_count,final_recipient_count,saw_data_before_recovery,saw_data_after_recovery,deferred_events,succeeded_events\nPASS,451,250," + initialEvidence.QueuedCount + "," + initialEvidence.RetryCount + "," + finalState.QueuedCount + "," + finalState.RecipientCount + "," + sink.SawDataBeforeRecovery + "," + sink.SawDataAfterRecovery + "," + deferredEvents + "," + report.succeededEvents + "\n");
        await File.WriteAllTextAsync(
            Path.ChangeExtension(fullPath, ".md"),
            $"# Net10 TCP 451 recovery\n\n- Result: `PASS`\n- SQL database: `{database}`\n- Data root: `{dataRoot}`\n- First attempt: `451`, queue=`{initialEvidence.QueuedCount}`, retry=`{initialEvidence.RetryCount}`, recipient=`{initialEvidence.RecipientCount}`, DATA before recovery=`{sink.SawDataBeforeRecovery}`\n- Recovery attempt: `250`, response observed=`{sink.SawRecoveryResponse}`, DATA observed=`{sink.SawDataAfterRecovery}`\n- Final state: queue=`{finalState.QueuedCount}`, recipients=`{finalState.RecipientCount}`, message file absent=`true`\n- Status events: deferred=`{deferredEvents}`, succeeded=`{report.succeededEvents}`\n\nThis is isolated Net10 retry-recovery evidence. It is not paired C++ evidence and does not clear the performance gate.\n\nJSON: `{fullPath}`\n");
    }

    private static double Percentile(double[] values, double percentile)
    {
        if (values.Length == 0) return 0;
        var rank = (percentile / 100d) * (values.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        return Math.Round(lower == upper ? values[lower] : values[lower] + ((values[upper] - values[lower]) * (rank - lower)), 3);
    }

    private sealed record MessageState(long QueuedCount, long DeliveredCount, long RecipientCount);

    private sealed record StoredMessage(long MessageId, string FileName, int AccountId, int FolderId);

    private sealed record RetryEvidence(
        long QueuedCount = 0,
        int MessageType = 0,
        int Locked = 0,
        int RetryCount = 0,
        DateTime NextTryUtc = default,
        bool LeaseOwnerIsNull = false,
        long RecipientCount = 0);

    private sealed record DeliveryBenchmarkReport(
        string Schema,
        DateTimeOffset GeneratedUtc,
        string Database,
        string DataRoot,
        int LocalMessageCount,
        int SampleCount,
        double TotalMilliseconds,
        double ThroughputMessagesPerSecond,
        double P50Milliseconds,
        double P95Milliseconds,
        double P99Milliseconds,
        RetryEvidence RetryEvidence);

    private sealed class FixedRetryTargetResolver : IDeliveryTargetResolver
    {
        public ValueTask<IReadOnlyList<DeliveryTargetBatch>> ResolveAsync(
            DeliveryQueuedMessage message,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DeliveryTargetBatch>>(
            [new DeliveryTargetBatch(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:retry.test", "retry.test"),
                message.Recipients)]);
    }

    private sealed class FixedEndpointResolver : IRemoteSmtpEndpointResolver
    {
        private readonly RemoteSmtpEndpoint _endpoint;

        public FixedEndpointResolver(RemoteSmtpEndpoint endpoint)
        {
            _endpoint = endpoint;
        }

        public ValueTask<RemoteSmtpEndpoint> ResolveAsync(
            DeliveryTarget target,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_endpoint);
    }

    private sealed class TransientSmtp451Sink : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _runTask;
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private TransientSmtp451Sink(TcpListener listener)
        {
            _listener = listener;
            _runTask = RunAsync();
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public Task Completion => _completion.Task;

        public bool SawEhloOrHelo { get; private set; }

        public bool SawMailFrom { get; private set; }

        public bool SawRecipient { get; private set; }

        public bool Saw451ResponseSent { get; private set; }

        public bool SawData { get; private set; }

        public static ValueTask<TransientSmtp451Sink> StartAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ValueTask.FromResult(new TransientSmtp451Sink(listener));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try
            {
                await _runTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 1024, leaveOpen: true);
                using var writer = new StreamWriter(client.GetStream(), Encoding.ASCII, 1024, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true
                };

                writer.WriteLine("220 loopback-retry.invalid ESMTP");
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is not null && (line.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("HELO ", StringComparison.OrdinalIgnoreCase)))
                {
                    SawEhloOrHelo = true;
                    writer.WriteLine("250 loopback-retry.invalid");
                }

                line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is not null && line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase))
                {
                    SawMailFrom = true;
                    writer.WriteLine("250 sender accepted");
                }

                line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is not null && line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
                {
                    SawRecipient = true;
                    Saw451ResponseSent = true;
                    writer.WriteLine("451 temporary recipient failure");
                }

                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
                {
                    if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        SawData = true;
                    }
                }

                _completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
        }
    }

    private sealed class StatefulSmtpRecoverySink : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _runTask;
        private readonly TaskCompletionSource<bool> _firstAttemptCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private StatefulSmtpRecoverySink(TcpListener listener)
        {
            _listener = listener;
            _runTask = RunAsync();
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public Task FirstAttemptCompletion => _firstAttemptCompletion.Task;

        public Task Completion => _completion.Task;

        public bool Saw451ResponseSent { get; private set; }

        public bool SawDataBeforeRecovery { get; private set; }

        public bool SawRecoveryResponse { get; private set; }

        public bool SawDataAfterRecovery { get; private set; }

        public static ValueTask<StatefulSmtpRecoverySink> StartAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ValueTask.FromResult(new StatefulSmtpRecoverySink(listener));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try
            {
                await _runTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private async Task RunAsync()
        {
            try
            {
                await RunFirstAttemptAsync().ConfigureAwait(false);
                _firstAttemptCompletion.TrySetResult(true);
                await RunRecoveryAttemptAsync().ConfigureAwait(false);
                _completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                _firstAttemptCompletion.TrySetException(exception);
                _completion.TrySetException(exception);
            }
        }

        private async Task RunFirstAttemptAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
            using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(client.GetStream(), Encoding.ASCII, 1024, leaveOpen: true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };

            writer.WriteLine("220 loopback-recovery.invalid ESMTP");
            await ReadAndReplyAsync(reader, writer, "250 loopback-recovery.invalid").ConfigureAwait(false);
            await ReadAndReplyAsync(reader, writer, "250 sender accepted").ConfigureAwait(false);
            var recipient = await reader.ReadLineAsync().ConfigureAwait(false);
            if (recipient is null || !recipient.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Recovery sink did not receive RCPT TO on the first attempt.");
            }

            writer.WriteLine("451 temporary recipient failure");
            Saw451ResponseSent = true;
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    SawDataBeforeRecovery = true;
                }
            }
        }

        private async Task RunRecoveryAttemptAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
            using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(client.GetStream(), Encoding.ASCII, 1024, leaveOpen: true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };

            writer.WriteLine("220 loopback-recovery.invalid ESMTP");
            await ReadAndReplyAsync(reader, writer, "250 loopback-recovery.invalid").ConfigureAwait(false);
            await ReadAndReplyAsync(reader, writer, "250 sender accepted").ConfigureAwait(false);
            var recipient = await reader.ReadLineAsync().ConfigureAwait(false);
            if (recipient is null || !recipient.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Recovery sink did not receive RCPT TO on the second attempt.");
            }

            writer.WriteLine("250 recipient accepted");
            SawRecoveryResponse = true;
            var dataCommand = await reader.ReadLineAsync().ConfigureAwait(false);
            if (!string.Equals(dataCommand, "DATA", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Recovery sink did not receive DATA after the successful RCPT.");
            }

            SawDataAfterRecovery = true;
            writer.WriteLine("354 start mail input");
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line && line != ".")
            {
            }

            writer.WriteLine("250 message accepted");
        }

        private static async Task ReadAndReplyAsync(StreamReader reader, StreamWriter writer, string response)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                throw new InvalidOperationException("Recovery sink received an incomplete SMTP command sequence.");
            }

            writer.WriteLine(response);
        }
    }

    private sealed class AlwaysTransientDispatcher : IDeliveryTargetDispatcher
    {
        public ValueTask<DeliveryTargetDispatchResult> DispatchAsync(
            DeliveryQueuedMessage message,
            DeliveryTargetBatch targetBatch,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(DeliveryTargetDispatchResult.TransientFailure("controlled disposable retry"));
    }

    private sealed class NoopBounceStore : IDeliveryBounceStore
    {
        public ValueTask<DeliveryBounceResult> SubmitBounceAsync(
            DeliveryQueuedMessage originalMessage,
            IReadOnlyList<DeliveryQueueRecipient> failedRecipients,
            string failureDescription,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(DeliveryBounceResult.Skipped("retry diagnostic does not submit bounces"));
    }

    private sealed class RecordingStatusObserver : IDeliveryQueueStatusObserver
    {
        public List<DeliveryQueueStatusEvent> Events { get; } = [];

        public ValueTask RecordAsync(DeliveryQueueStatusEvent statusEvent, CancellationToken cancellationToken)
        {
            Events.Add(statusEvent);
            return ValueTask.CompletedTask;
        }
    }
}
