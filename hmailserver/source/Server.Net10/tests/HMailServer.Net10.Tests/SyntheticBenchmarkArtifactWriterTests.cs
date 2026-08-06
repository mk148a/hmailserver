using System.Text.Json;
using HMailServer.Net10.Benchmarks;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SyntheticBenchmarkArtifactWriterTests
{
    [TestMethod]
    public void Write_EmitsJsonCsvAndMarkdownArtifacts()
    {
        var report = new SyntheticBenchmarkReport(
            Scenario: "offline-imap-search-sort",
            StartedUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndedUtc: new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero),
            GitCommit: "abc123",
            OsDescription: "Test OS",
            RuntimeDescription: "Test Runtime",
            ProcessArchitecture: "x64",
            ProcessorCount: 8,
            MessageCount: 100_000,
            Seed: 5700,
            WarmupIterations: 2,
            MeasuredIterations: 7,
            SearchTerm: SyntheticImapSearchSortBenchmark.SearchTerm,
            SortOrder: SyntheticImapSearchSortBenchmark.SortOrder,
            ExpectedMatchCount: 1,
            ActualMatchCount: 1,
            UniqueResultIds: true,
            CorrectSortOrder: true,
            Correct: true,
            P95ThresholdMilliseconds: 2_500,
            ThresholdPassed: true,
            Metrics: new SyntheticBenchmarkMetric(1.0, 2.0, 3.0, 1.5, 1000, 2048)
            {
                MeanGen0Collections = 1,
                MeanGen1Collections = 0,
                MeanGen2Collections = 0,
                PeakWorkingSetBytes = 1234
            },
            FirstResultIds: new long[] { 1, 2 });

        var directory = Path.Combine(Path.GetTempPath(), "hmailserver-net10-bench-artifacts-" + Guid.NewGuid().ToString("N"));
        try
        {
            SyntheticBenchmarkArtifactWriter.Write(report, directory);

            var jsonPath = Path.Combine(directory, "offline-imap-search-sort.json");
            var csvPath = Path.Combine(directory, "offline-imap-search-sort.csv");
            var mdPath = Path.Combine(directory, "offline-imap-search-sort.md");
            Assert.IsTrue(File.Exists(jsonPath));
            Assert.IsTrue(File.Exists(csvPath));
            Assert.IsTrue(File.Exists(mdPath));

            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            Assert.AreEqual("offline-imap-search-sort", document.RootElement.GetProperty("Scenario").GetString());

            var csv = File.ReadAllText(csvPath);
            StringAssert.Contains(csv, "scenario,git_commit,message_count,seed,expected_matches,actual_matches,correct");
            StringAssert.Contains(csv, "offline-imap-search-sort");
            StringAssert.Contains(csv, ",1,1,True,1,2,3,1.5,1000,2048,1,0,0,2500,True,1234");

            var md = File.ReadAllText(mdPath);
            StringAssert.Contains(md, "| p50 | 1 ms |");
            StringAssert.Contains(md, "| p95 | 2 ms |");
            StringAssert.Contains(md, "| Correctness | `True` (1 matches) |");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}