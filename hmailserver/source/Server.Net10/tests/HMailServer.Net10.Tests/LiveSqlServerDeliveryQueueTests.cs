using System.Diagnostics;
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
            "SELECT COUNT_BIG(*), MAX(CAST(messagelocked AS int)), MAX(messagecurnooftries), MAX(messagenexttrytime), MAX(CASE WHEN messageleaseowner IS NULL THEN 1 ELSE 0 END) FROM hm_messages WHERE messagefrom = @MessageFrom; SELECT COUNT_BIG(*) FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid = r.recipientmessageid WHERE m.messagefrom = @MessageFrom;",
            connection);
        command.Parameters.AddWithValue("@MessageFrom", marker);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var evidence = new RetryEvidence(
            Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(2), System.Globalization.CultureInfo.InvariantCulture),
            DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc),
            Convert.ToInt32(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture) == 1);
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
