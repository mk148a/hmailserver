using System.Text.Json;
using HMailServer.Net10.Benchmarks;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SyntheticImapSearchSortBenchmarkTests
{
    [TestMethod]
    public void SameSeedProducesSameSearchAndSortResult()
    {
        var first = SyntheticImapSearchSortBenchmark.CreateDataset(2_000, 5700);
        var second = SyntheticImapSearchSortBenchmark.CreateDataset(2_000, 5700);

        var firstReport = SyntheticImapSearchSortBenchmark.Run(
            first,
            new SyntheticImapBenchmarkOptions(WarmupIterations: 0, MeasuredIterations: 2));
        var secondReport = SyntheticImapSearchSortBenchmark.Run(
            second,
            new SyntheticImapBenchmarkOptions(WarmupIterations: 0, MeasuredIterations: 2));

        Assert.AreEqual(firstReport.ExpectedMatchCount, secondReport.ExpectedMatchCount);
        CollectionAssert.AreEqual(firstReport.FirstResultIds.ToArray(), secondReport.FirstResultIds.ToArray());
        Assert.IsTrue(firstReport.Correct);
        Assert.IsTrue(secondReport.Correct);
    }

    [TestMethod]
    public void ArtifactWriterEmitsJsonCsvAndMarkdownWithAcceptanceFields()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "hmailserver-net10-benchmark-" + Guid.NewGuid().ToString("N"));
        try
        {
            var report = SyntheticImapSearchSortBenchmark.Run(
                SyntheticImapSearchSortBenchmark.CreateDataset(1_000, 5700),
                new SyntheticImapBenchmarkOptions(WarmupIterations: 0, MeasuredIterations: 2));

            SyntheticBenchmarkArtifactWriter.Write(report, outputDirectory);

            var jsonPath = Path.Combine(outputDirectory, "offline-imap-search-sort.json");
            var csvPath = Path.Combine(outputDirectory, "offline-imap-search-sort.csv");
            var markdownPath = Path.Combine(outputDirectory, "offline-imap-search-sort.md");
            Assert.IsTrue(File.Exists(jsonPath));
            Assert.IsTrue(File.Exists(csvPath));
            Assert.IsTrue(File.Exists(markdownPath));
            Assert.IsNotNull(JsonSerializer.Deserialize<SyntheticBenchmarkReport>(File.ReadAllText(jsonPath)));
            StringAssert.Contains(File.ReadAllText(csvPath), "p50_ms,p95_ms,p99_ms");
            StringAssert.Contains(File.ReadAllText(markdownPath), "offline-imap-search-sort-100k");
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
