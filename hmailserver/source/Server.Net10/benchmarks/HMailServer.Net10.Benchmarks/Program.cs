using HMailServer.Net10.Benchmarks;

var options = ParseOptions(args);
var dataset = SyntheticImapSearchSortBenchmark.CreateDataset(options.MessageCount, options.Seed);
if (string.Equals(ParseString(args, "--mode", "search-sort"), "short-soak", StringComparison.OrdinalIgnoreCase))
{
    var soak = ShortSoakBenchmark.Run(
        dataset,
        new ShortSoakBenchmarkOptions(
            MessageCount: options.MessageCount,
            Cycles: ParseInt(args, "--cycles", 20),
            Seed: options.Seed,
            MaxDurationSeconds: ParseInt(args, "--max-seconds", 30),
            P95ThresholdMilliseconds: ParseDouble(args, "--p95-threshold-ms", 2_500),
            MaxPrivateMemoryGrowthBytes: ParseLong(args, "--max-private-memory-growth-bytes", 64 * 1024 * 1024),
            MaxHandleGrowth: ParseInt(args, "--max-handle-growth", 100),
            MaxThreadGrowth: ParseInt(args, "--max-thread-growth", 20),
            MaxTcpConnectionGrowth: ParseInt(args, "--max-tcp-growth", 50),
            GitCommit: options.GitCommit));
    var soakOutputDirectory = ParseOutputDirectory(args);
    ShortSoakArtifactWriter.Write(soak, soakOutputDirectory);
    Console.WriteLine($"Wrote short-soak artifacts to {Path.GetFullPath(soakOutputDirectory)}");
    Console.WriteLine($"cycles={soak.CompletedCycles}/{soak.AttemptedCycles} errors={soak.ErrorCount} p95={soak.P95Milliseconds:0.###}ms threshold={soak.ThresholdPassed}");
    return soak.Correct && soak.ThresholdPassed ? 0 : 2;
}

var report = SyntheticImapSearchSortBenchmark.Run(dataset, options);
var outputDirectory = ParseOutputDirectory(args);
SyntheticBenchmarkArtifactWriter.Write(report, outputDirectory);
Console.WriteLine($"Wrote offline SEARCH/SORT artifacts to {Path.GetFullPath(outputDirectory)}");
Console.WriteLine($"p50={report.Metrics.P50Milliseconds:0.###}ms p95={report.Metrics.P95Milliseconds:0.###}ms p99={report.Metrics.P99Milliseconds:0.###}ms correct={report.Correct} threshold={report.ThresholdPassed}");

return report.Correct && report.ThresholdPassed ? 0 : 2;

static SyntheticImapBenchmarkOptions ParseOptions(string[] args)
{
    return new SyntheticImapBenchmarkOptions(
        MessageCount: ParseInt(args, "--count", 100_000),
        WarmupIterations: ParseInt(args, "--warmup", 2),
        MeasuredIterations: ParseInt(args, "--iterations", 7),
        Seed: ParseInt(args, "--seed", 5700),
        GitCommit: ParseString(args, "--git-commit", Environment.GetEnvironmentVariable("GIT_COMMIT") ?? "unknown"),
        P95ThresholdMilliseconds: ParseDouble(args, "--p95-threshold-ms", 2_500));
}

static string ParseOutputDirectory(string[] args) =>
    ParseString(args, "--output", Path.Combine("artifacts", "benchmarks"));

static int ParseInt(string[] args, string name, int fallback) =>
    int.TryParse(ParseOptional(args, name), out var value) ? value : fallback;

static double ParseDouble(string[] args, string name, double fallback) =>
    double.TryParse(ParseOptional(args, name), out var value) ? value : fallback;

static long ParseLong(string[] args, string name, long fallback) =>
    long.TryParse(ParseOptional(args, name), out var value) ? value : fallback;

static string ParseString(string[] args, string name, string fallback) =>
    ParseOptional(args, name) ?? fallback;

static string? ParseOptional(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
