using System.Text.Json;
using HMailServer.Net10.Benchmarks;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ShortSoakBenchmarkTests
{
    [TestMethod]
    public void Run_CompletesCorrectOfflineCyclesAndReportsProcessDeltas()
    {
        var dataset = SyntheticImapSearchSortBenchmark.CreateDataset(500, 5700);
        var report = ShortSoakBenchmark.Run(
            dataset,
            new ShortSoakBenchmarkOptions(
                MessageCount: dataset.Count,
                Cycles: 2,
                MaxDurationSeconds: 30,
                P95ThresholdMilliseconds: double.MaxValue,
                MaxPrivateMemoryGrowthBytes: long.MaxValue,
                MaxHandleGrowth: int.MaxValue,
                MaxThreadGrowth: int.MaxValue,
                MaxTcpConnectionGrowth: int.MaxValue,
                GitCommit: "test"));

        Assert.AreEqual(2, report.AttemptedCycles);
        Assert.AreEqual(2, report.CompletedCycles);
        Assert.AreEqual(0, report.ErrorCount);
        Assert.IsTrue(report.Correct);
        Assert.IsTrue(report.ThresholdPassed);
        Assert.AreEqual("test", report.GitCommit);
    }

    [TestMethod]
    public void Run_FailsThresholdWhenLatencyLimitIsZero()
    {
        var dataset = SyntheticImapSearchSortBenchmark.CreateDataset(100, 5700);
        var report = ShortSoakBenchmark.Run(
            dataset,
            new ShortSoakBenchmarkOptions(
                MessageCount: dataset.Count,
                Cycles: 1,
                MaxDurationSeconds: 30,
                P95ThresholdMilliseconds: 0,
                MaxPrivateMemoryGrowthBytes: long.MaxValue,
                MaxHandleGrowth: int.MaxValue,
                MaxThreadGrowth: int.MaxValue,
                MaxTcpConnectionGrowth: int.MaxValue));

        Assert.IsTrue(report.Correct);
        Assert.IsFalse(report.ThresholdPassed);
    }

    [TestMethod]
    public void Run_FailsCorrectnessWhenDurationStopsBeforeRequestedCycles()
    {
        var dataset = SyntheticImapSearchSortBenchmark.CreateDataset(100, 5700);
        var clock = new AdvancingTimeProvider(
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(2));

        var report = ShortSoakBenchmark.Run(
            dataset,
            new ShortSoakBenchmarkOptions(
                MessageCount: dataset.Count,
                Cycles: 2,
                MaxDurationSeconds: 1,
                P95ThresholdMilliseconds: double.MaxValue,
                MaxPrivateMemoryGrowthBytes: long.MaxValue,
                MaxHandleGrowth: int.MaxValue,
                MaxThreadGrowth: int.MaxValue,
                MaxTcpConnectionGrowth: int.MaxValue,
                Clock: clock));

        Assert.AreEqual(1, report.AttemptedCycles);
        Assert.AreEqual(1, report.CompletedCycles);
        Assert.IsFalse(report.Correct);
        Assert.IsFalse(report.ThresholdPassed);
    }

    [TestMethod]
    public void Writer_EmitsJsonCsvAndMarkdownWithLeakMetrics()
    {
        var dataset = SyntheticImapSearchSortBenchmark.CreateDataset(100, 5700);
        var report = ShortSoakBenchmark.Run(
            dataset,
            new ShortSoakBenchmarkOptions(
                MessageCount: dataset.Count,
                Cycles: 1,
                MaxDurationSeconds: 30,
                P95ThresholdMilliseconds: double.MaxValue,
                MaxPrivateMemoryGrowthBytes: long.MaxValue,
                MaxHandleGrowth: int.MaxValue,
                MaxThreadGrowth: int.MaxValue,
                MaxTcpConnectionGrowth: int.MaxValue));
        var outputDirectory = Path.Combine(Path.GetTempPath(), "hmailserver-net10-short-soak-" + Guid.NewGuid().ToString("N"));

        try
        {
            ShortSoakArtifactWriter.Write(report, outputDirectory);

            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "short-soak.json")));
            Assert.AreEqual("short-offline-imap-search-sort-soak", json.RootElement.GetProperty("Scenario").GetString());
            StringAssert.Contains(File.ReadAllText(Path.Combine(outputDirectory, "short-soak.csv")), "private_memory_growth_bytes");
            StringAssert.Contains(File.ReadAllText(Path.Combine(outputDirectory, "short-soak.md")), "TCP connection growth");
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private sealed class AdvancingTimeProvider(
        DateTimeOffset current,
        TimeSpan advancePerRead) : TimeProvider
    {
        private DateTimeOffset _current = current;
        private int _reads;

        public override DateTimeOffset GetUtcNow()
        {
            var value = _current;
            if (Interlocked.Increment(ref _reads) > 1)
                _current += advancePerRead;
            return value;
        }
    }
}
