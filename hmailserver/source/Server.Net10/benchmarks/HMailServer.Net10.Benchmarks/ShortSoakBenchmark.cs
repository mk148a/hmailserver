using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HMailServer.Net10.Benchmarks;

public sealed record ShortSoakBenchmarkOptions(
    int MessageCount = 100_000,
    int Cycles = 20,
    int Seed = 5700,
    int MaxDurationSeconds = 30,
    double P95ThresholdMilliseconds = 2_500,
    long MaxPrivateMemoryGrowthBytes = 64 * 1024 * 1024,
    int MaxHandleGrowth = 100,
    int MaxThreadGrowth = 20,
    int MaxTcpConnectionGrowth = 50,
    string GitCommit = "unknown");

public sealed record ShortSoakProcessSnapshot(
    long PrivateMemoryBytes,
    long WorkingSetBytes,
    int HandleCount,
    int ThreadCount,
    int ActiveTcpConnectionCount,
    long Gen0Collections,
    long Gen1Collections,
    long Gen2Collections);

public sealed record ShortSoakBenchmarkReport(
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
    int RequestedCycles,
    int AttemptedCycles,
    int CompletedCycles,
    int ErrorCount,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    ShortSoakProcessSnapshot StartProcess,
    ShortSoakProcessSnapshot EndProcess,
    long PrivateMemoryGrowthBytes,
    long WorkingSetGrowthBytes,
    int HandleGrowth,
    int ThreadGrowth,
    int TcpConnectionGrowth,
    long Gen0Growth,
    long Gen1Growth,
    long Gen2Growth,
    double P95ThresholdMilliseconds,
    long MaxPrivateMemoryGrowthBytes,
    int MaxHandleGrowth,
    int MaxThreadGrowth,
    int MaxTcpConnectionGrowth,
    bool Correct,
    bool ThresholdPassed);

public static class ShortSoakBenchmark
{
    public static ShortSoakBenchmarkReport Run(
        IReadOnlyList<SyntheticImapMessage> messages,
        ShortSoakBenchmarkOptions options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);
        if (messages.Count == 0)
            throw new ArgumentException("The soak dataset cannot be empty.", nameof(messages));
        if (options.Cycles < 1 || options.MaxDurationSeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "Soak cycle and duration limits must be positive.");

        var startedUtc = DateTimeOffset.UtcNow;
        var startProcess = CaptureProcessSnapshot();
        var latencies = new List<double>(options.Cycles);
        var attempted = 0;
        var errors = 0;

        for (; attempted < options.Cycles; attempted++)
        {
            if (DateTimeOffset.UtcNow - startedUtc >= TimeSpan.FromSeconds(options.MaxDurationSeconds))
                break;

            try
            {
                var report = SyntheticImapSearchSortBenchmark.Run(
                    messages,
                    new SyntheticImapBenchmarkOptions(
                        MessageCount: messages.Count,
                        WarmupIterations: 0,
                        MeasuredIterations: 1,
                        Seed: options.Seed,
                        GitCommit: options.GitCommit,
                        P95ThresholdMilliseconds: double.MaxValue));
                if (!report.Correct)
                {
                    errors++;
                    continue;
                }

                latencies.Add(report.Metrics.P95Milliseconds);
            }
            catch
            {
                errors++;
            }
        }

        var endedUtc = DateTimeOffset.UtcNow;
        var endProcess = CaptureProcessSnapshot();
        var p50 = Percentile(latencies, 0.50);
        var p95 = Percentile(latencies, 0.95);
        var p99 = Percentile(latencies, 0.99);
        var privateMemoryGrowth = endProcess.PrivateMemoryBytes - startProcess.PrivateMemoryBytes;
        var workingSetGrowth = endProcess.WorkingSetBytes - startProcess.WorkingSetBytes;
        var handleGrowth = endProcess.HandleCount - startProcess.HandleCount;
        var threadGrowth = endProcess.ThreadCount - startProcess.ThreadCount;
        var tcpGrowth = endProcess.ActiveTcpConnectionCount - startProcess.ActiveTcpConnectionCount;
        var gen0Growth = endProcess.Gen0Collections - startProcess.Gen0Collections;
        var gen1Growth = endProcess.Gen1Collections - startProcess.Gen1Collections;
        var gen2Growth = endProcess.Gen2Collections - startProcess.Gen2Collections;
        var correct = errors == 0 && latencies.Count == attempted && attempted > 0;
        var thresholdPassed = correct
            && p95 <= options.P95ThresholdMilliseconds
            && privateMemoryGrowth <= options.MaxPrivateMemoryGrowthBytes
            && handleGrowth <= options.MaxHandleGrowth
            && threadGrowth <= options.MaxThreadGrowth
            && tcpGrowth <= options.MaxTcpConnectionGrowth;

        return new ShortSoakBenchmarkReport(
            Scenario: "short-offline-imap-search-sort-soak",
            StartedUtc: startedUtc,
            EndedUtc: endedUtc,
            GitCommit: options.GitCommit,
            OsDescription: RuntimeInformation.OSDescription,
            RuntimeDescription: RuntimeInformation.FrameworkDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount: Environment.ProcessorCount,
            MessageCount: messages.Count,
            Seed: options.Seed,
            RequestedCycles: options.Cycles,
            AttemptedCycles: attempted,
            CompletedCycles: latencies.Count,
            ErrorCount: errors,
            P50Milliseconds: p50,
            P95Milliseconds: p95,
            P99Milliseconds: p99,
            StartProcess: startProcess,
            EndProcess: endProcess,
            PrivateMemoryGrowthBytes: privateMemoryGrowth,
            WorkingSetGrowthBytes: workingSetGrowth,
            HandleGrowth: handleGrowth,
            ThreadGrowth: threadGrowth,
            TcpConnectionGrowth: tcpGrowth,
            Gen0Growth: gen0Growth,
            Gen1Growth: gen1Growth,
            Gen2Growth: gen2Growth,
            P95ThresholdMilliseconds: options.P95ThresholdMilliseconds,
            MaxPrivateMemoryGrowthBytes: options.MaxPrivateMemoryGrowthBytes,
            MaxHandleGrowth: options.MaxHandleGrowth,
            MaxThreadGrowth: options.MaxThreadGrowth,
            MaxTcpConnectionGrowth: options.MaxTcpConnectionGrowth,
            Correct: correct,
            ThresholdPassed: thresholdPassed);
    }

    private static ShortSoakProcessSnapshot CaptureProcessSnapshot()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return new ShortSoakProcessSnapshot(
            PrivateMemoryBytes: process.PrivateMemorySize64,
            WorkingSetBytes: process.WorkingSet64,
            HandleCount: process.HandleCount,
            ThreadCount: process.Threads.Count,
            ActiveTcpConnectionCount: IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Length,
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2));
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
            return double.PositiveInfinity;

        var ordered = values.OrderBy(static value => value).ToArray();
        var position = (ordered.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper
            ? ordered[lower]
            : ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower);
    }
}

public static class ShortSoakArtifactWriter
{
    public static void Write(ShortSoakBenchmarkReport report, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(outputDirectory);
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(outputDirectory, "short-soak.json"),
            JsonSerializer.Serialize(report, options));
        File.WriteAllText(Path.Combine(outputDirectory, "short-soak.csv"), CreateCsv(report));
        File.WriteAllText(Path.Combine(outputDirectory, "short-soak.md"), CreateMarkdown(report));
    }

    private static string CreateCsv(ShortSoakBenchmarkReport report) => string.Join(
        Environment.NewLine,
        "scenario,git_commit,message_count,requested_cycles,attempted_cycles,completed_cycles,errors,p50_ms,p95_ms,p99_ms,private_memory_growth_bytes,working_set_growth_bytes,handle_growth,thread_growth,tcp_connection_growth,gen0_growth,gen1_growth,gen2_growth,threshold_passed",
        string.Join(
            ",",
            Csv(report.Scenario),
            Csv(report.GitCommit),
            report.MessageCount,
            report.RequestedCycles,
            report.AttemptedCycles,
            report.CompletedCycles,
            report.ErrorCount,
            Number(report.P50Milliseconds),
            Number(report.P95Milliseconds),
            Number(report.P99Milliseconds),
            report.PrivateMemoryGrowthBytes,
            report.WorkingSetGrowthBytes,
            report.HandleGrowth,
            report.ThreadGrowth,
            report.TcpConnectionGrowth,
            report.Gen0Growth,
            report.Gen1Growth,
            report.Gen2Growth,
            report.ThresholdPassed));

    private static string CreateMarkdown(ShortSoakBenchmarkReport report) => string.Join(
        Environment.NewLine,
        "# Short Soak Acceptance",
        string.Empty,
        "| Field | Value |",
        "| --- | --- |",
        $"| Scenario | `{report.Scenario}` |",
        $"| Git commit | `{report.GitCommit}` |",
        $"| Cycles | {report.CompletedCycles}/{report.AttemptedCycles} completed of {report.RequestedCycles} requested |",
        $"| Errors | {report.ErrorCount} |",
        $"| p50 / p95 / p99 | {Number(report.P50Milliseconds)} / {Number(report.P95Milliseconds)} / {Number(report.P99Milliseconds)} ms |",
        $"| Private memory growth | {report.PrivateMemoryGrowthBytes.ToString("N0", CultureInfo.InvariantCulture)} bytes (limit {report.MaxPrivateMemoryGrowthBytes.ToString("N0", CultureInfo.InvariantCulture)}) |",
        $"| Handle growth | {report.HandleGrowth} (limit {report.MaxHandleGrowth}) |",
        $"| Thread growth | {report.ThreadGrowth} (limit {report.MaxThreadGrowth}) |",
        $"| TCP connection growth | {report.TcpConnectionGrowth} (limit {report.MaxTcpConnectionGrowth}) |",
        $"| GC growth | Gen0 {report.Gen0Growth}, Gen1 {report.Gen1Growth}, Gen2 {report.Gen2Growth} |",
        $"| Threshold | `{report.ThresholdPassed}` |",
        $"| Window | {report.StartedUtc:O} to {report.EndedUtc:O} |",
        string.Empty,
        "This is a short offline synthetic soak. It does not prove a 24-hour service leak-free run, live protocol equivalence, SQL behavior, COM lifecycle, or production readiness.");

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
