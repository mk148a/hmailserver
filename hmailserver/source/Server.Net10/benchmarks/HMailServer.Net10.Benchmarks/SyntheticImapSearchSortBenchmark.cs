using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HMailServer.Net10.Benchmarks;

public sealed record SyntheticImapBenchmarkOptions(
    int MessageCount = 100_000,
    int WarmupIterations = 2,
    int MeasuredIterations = 7,
    int Seed = 5700,
    string GitCommit = "unknown",
    double P95ThresholdMilliseconds = 2_500);

public sealed record SyntheticImapMessage(
    long MessageId,
    long Uid,
    DateTime ArrivalUtc,
    DateTime SentUtc,
    int SizeBytes,
    string From,
    string To,
    string Subject,
    string Body);

public sealed record SyntheticBenchmarkMetric(
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MeanMilliseconds,
    double ThroughputMessagesPerSecond,
    long MeanAllocatedBytes);

public sealed record SyntheticBenchmarkReport(
    string Scenario,
    DateTimeOffset StartedUtc,
    DateTimeOffset EndedUtc,
    string GitCommit,
    string OsDescription,
    string RuntimeDescription,
    string ProcessArchitecture,
    int ProcessorCount,
    int MessageCount,
    int Seed,
    int WarmupIterations,
    int MeasuredIterations,
    string SearchTerm,
    string SortOrder,
    int ExpectedMatchCount,
    int ActualMatchCount,
    bool UniqueResultIds,
    bool CorrectSortOrder,
    bool Correct,
    double P95ThresholdMilliseconds,
    bool ThresholdPassed,
    SyntheticBenchmarkMetric Metrics,
    IReadOnlyList<long> FirstResultIds);

public static class SyntheticImapSearchSortBenchmark
{
    public const string SearchTerm = "needle";
    public const string SortOrder = "DATE DESC, UID ASC";

    public static IReadOnlyList<SyntheticImapMessage> CreateDataset(int messageCount, int seed)
    {
        if (messageCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(messageCount));
        }

        var random = new Random(seed);
        var messages = new SyntheticImapMessage[messageCount];
        var baseTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < messages.Length; index++)
        {
            var receivedMinutes = (index * 37L) % 525_600L;
            var sentMinutes = (index * 53L) % 525_600L;
            var hasNeedle = index % 11 == 0;
            var sender = index % 257;
            var subjectBucket = index % 1_003;
            messages[index] = new SyntheticImapMessage(
                MessageId: index + 1,
                Uid: index + 1,
                ArrivalUtc: baseTime.AddMinutes(receivedMinutes),
                SentUtc: baseTime.AddMinutes(sentMinutes),
                SizeBytes: 512 + random.Next(1, 250_000),
                From: $"sender-{sender:D3}@example.test",
                To: $"recipient-{index % 1_009:D4}@example.test",
                Subject: hasNeedle
                    ? $"archive needle {subjectBucket:D4}"
                    : $"archive {subjectBucket:D4}",
                Body: hasNeedle
                    ? "deterministic benchmark needle body"
                    : "deterministic benchmark body");
        }

        return messages;
    }

    public static SyntheticBenchmarkReport Run(
        IReadOnlyList<SyntheticImapMessage> messages,
        SyntheticImapBenchmarkOptions options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);
        if (messages.Count == 0)
        {
            throw new ArgumentException("The benchmark dataset cannot be empty.", nameof(messages));
        }
        if (options.WarmupIterations < 0 || options.MeasuredIterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Iteration counts are invalid.");
        }

        var expected = Execute(messages);
        var samples = new List<double>(options.MeasuredIterations);
        var allocations = new List<long>(options.MeasuredIterations);
        var actualMatchCount = 0;
        var startedUtc = DateTimeOffset.UtcNow;

        for (var iteration = 0; iteration < options.WarmupIterations; iteration++)
        {
            _ = Execute(messages);
        }

        for (var iteration = 0; iteration < options.MeasuredIterations; iteration++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            var actual = Execute(messages);
            stopwatch.Stop();
            var afterAllocated = GC.GetAllocatedBytesForCurrentThread();

            if (!MatchesExpected(actual, expected))
            {
                throw new InvalidOperationException("Synthetic SEARCH/SORT result changed between iterations.");
            }

            actualMatchCount = actual.Length;
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            allocations.Add(afterAllocated - beforeAllocated);
        }

        var endedUtc = DateTimeOffset.UtcNow;
        var metrics = CreateMetrics(samples, allocations, expected.Length);
        var uniqueResultIds = expected.Select(static message => message.Uid).Distinct().Count() == expected.Length;
        var correctSortOrder = IsSorted(expected);
        var thresholdPassed = metrics.P95Milliseconds <= options.P95ThresholdMilliseconds;

        return new SyntheticBenchmarkReport(
            Scenario: "offline-imap-search-sort-100k",
            StartedUtc: startedUtc,
            EndedUtc: endedUtc,
            GitCommit: options.GitCommit,
            OsDescription: RuntimeInformation.OSDescription,
            RuntimeDescription: RuntimeInformation.FrameworkDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount: Environment.ProcessorCount,
            MessageCount: messages.Count,
            Seed: options.Seed,
            WarmupIterations: options.WarmupIterations,
            MeasuredIterations: options.MeasuredIterations,
            SearchTerm: SearchTerm,
            SortOrder: SortOrder,
            ExpectedMatchCount: expected.Length,
            ActualMatchCount: actualMatchCount,
            UniqueResultIds: uniqueResultIds,
            CorrectSortOrder: correctSortOrder,
            Correct: expected.Length == actualMatchCount && uniqueResultIds && correctSortOrder,
            P95ThresholdMilliseconds: options.P95ThresholdMilliseconds,
            ThresholdPassed: thresholdPassed,
            Metrics: metrics,
            FirstResultIds: expected.Take(10).Select(static message => message.Uid).ToArray());
    }

    private static SyntheticImapMessage[] Execute(IReadOnlyList<SyntheticImapMessage> messages) =>
        messages
            .Where(static message =>
                message.Subject.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                message.Body.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                message.From.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                message.To.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static message => message.SentUtc)
            .ThenBy(static message => message.Uid)
            .ToArray();

    private static bool MatchesExpected(
        IReadOnlyList<SyntheticImapMessage> actual,
        IReadOnlyList<SyntheticImapMessage> expected) =>
        actual.Count == expected.Count && actual.Select(static message => message.Uid).SequenceEqual(
            expected.Select(static message => message.Uid));

    private static bool IsSorted(IReadOnlyList<SyntheticImapMessage> messages)
    {
        for (var index = 1; index < messages.Count; index++)
        {
            var previous = messages[index - 1];
            var current = messages[index];
            if (previous.SentUtc < current.SentUtc ||
                (previous.SentUtc == current.SentUtc && previous.Uid > current.Uid))
            {
                return false;
            }
        }

        return true;
    }

    private static SyntheticBenchmarkMetric CreateMetrics(
        IReadOnlyList<double> samples,
        IReadOnlyList<long> allocations,
        int resultCount)
    {
        var ordered = samples.OrderBy(static value => value).ToArray();
        var mean = samples.Average();
        return new SyntheticBenchmarkMetric(
            P50Milliseconds: Percentile(ordered, 0.50),
            P95Milliseconds: Percentile(ordered, 0.95),
            P99Milliseconds: Percentile(ordered, 0.99),
            MeanMilliseconds: mean,
            ThroughputMessagesPerSecond: resultCount / (mean / 1_000),
            MeanAllocatedBytes: (long)allocations.Average());
    }

    private static double Percentile(IReadOnlyList<double> ordered, double percentile)
    {
        var position = (ordered.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return ordered[lower];
        }

        return ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower);
    }
}

public static class SyntheticBenchmarkArtifactWriter
{
    public static void Write(SyntheticBenchmarkReport report, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(outputDirectory);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(outputDirectory, "offline-imap-search-sort.json"),
            JsonSerializer.Serialize(report, jsonOptions));
        File.WriteAllText(Path.Combine(outputDirectory, "offline-imap-search-sort.csv"), CreateCsv(report));
        File.WriteAllText(Path.Combine(outputDirectory, "offline-imap-search-sort.md"), CreateMarkdown(report));
    }

    private static string CreateCsv(SyntheticBenchmarkReport report) =>
        string.Join(Environment.NewLine,
        "scenario,git_commit,message_count,seed,expected_matches,actual_matches,correct,p50_ms,p95_ms,p99_ms,mean_ms,throughput_messages_per_second,mean_allocated_bytes,threshold_ms,threshold_passed",
        string.Join(",", Csv(report.Scenario), Csv(report.GitCommit), report.MessageCount.ToString(CultureInfo.InvariantCulture), report.Seed.ToString(CultureInfo.InvariantCulture), report.ExpectedMatchCount.ToString(CultureInfo.InvariantCulture), report.ActualMatchCount.ToString(CultureInfo.InvariantCulture), report.Correct.ToString(CultureInfo.InvariantCulture), Number(report.Metrics.P50Milliseconds), Number(report.Metrics.P95Milliseconds), Number(report.Metrics.P99Milliseconds), Number(report.Metrics.MeanMilliseconds), Number(report.Metrics.ThroughputMessagesPerSecond), report.Metrics.MeanAllocatedBytes.ToString(CultureInfo.InvariantCulture), Number(report.P95ThresholdMilliseconds), report.ThresholdPassed.ToString(CultureInfo.InvariantCulture)));

    private static string CreateMarkdown(SyntheticBenchmarkReport report) => string.Join(
        Environment.NewLine,
        "# Offline IMAP SEARCH/SORT",
        string.Empty,
        "| Field | Value |",
        "| --- | --- |",
        $"| Scenario | `{report.Scenario}` |",
        $"| Git commit | `{report.GitCommit}` |",
        $"| Dataset | {report.MessageCount.ToString("N0", CultureInfo.InvariantCulture)} messages, seed `{report.Seed}` |",
        $"| Search | `{report.SearchTerm}` |",
        $"| Sort | `{report.SortOrder}` |",
        $"| Correctness | `{report.Correct}` ({report.ExpectedMatchCount} matches) |",
        $"| p50 | {Number(report.Metrics.P50Milliseconds)} ms |",
        $"| p95 | {Number(report.Metrics.P95Milliseconds)} ms |",
        $"| p99 | {Number(report.Metrics.P99Milliseconds)} ms |",
        $"| Throughput | {Number(report.Metrics.ThroughputMessagesPerSecond)} messages/s |",
        $"| Mean allocation | {report.Metrics.MeanAllocatedBytes.ToString("N0", CultureInfo.InvariantCulture)} bytes |",
        $"| p95 threshold | {Number(report.P95ThresholdMilliseconds)} ms (`{report.ThresholdPassed}`) |",
        $"| Host | {report.OsDescription}; {report.RuntimeDescription}; {report.ProcessArchitecture}; {report.ProcessorCount} logical processors |",
        $"| Window | {report.StartedUtc:O} to {report.EndedUtc:O} |",
        string.Empty,
        $"First result UIDs: `{string.Join(", ", report.FirstResultIds)}`",
        string.Empty,
        "This is an offline synthetic acceptance harness. It does not prove SQL Server FTS, live IMAP protocol latency, or legacy C++ performance equivalence.");

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
