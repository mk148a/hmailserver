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
    public void ActualMatchCountReportsExecutedSearchResultCount()
    {
        var report = SyntheticImapSearchSortBenchmark.Run(
            SyntheticImapSearchSortBenchmark.CreateDataset(23, 5700),
            new SyntheticImapBenchmarkOptions(WarmupIterations: 0, MeasuredIterations: 1));

        Assert.AreEqual(3, report.ActualMatchCount);
        Assert.IsTrue(report.Correct);
        Assert.IsTrue(report.Metrics.MeanGen0Collections >= 0);
        Assert.IsTrue(report.Metrics.MeanGen1Collections >= 0);
        Assert.IsTrue(report.Metrics.MeanGen2Collections >= 0);
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
            var json = File.ReadAllText(jsonPath);
            Assert.IsNotNull(JsonSerializer.Deserialize<SyntheticBenchmarkReport>(json));
            StringAssert.Contains(json, "\"MeanGen0Collections\"");
            StringAssert.Contains(json, "\"MeanGen1Collections\"");
            StringAssert.Contains(json, "\"MeanGen2Collections\"");
            var csv = File.ReadAllText(csvPath);
            var markdown = File.ReadAllText(markdownPath);
            StringAssert.Contains(csv, "mean_gen0_collections,mean_gen1_collections,mean_gen2_collections");
            StringAssert.Contains(markdown, "Mean Gen 0 collections");
            StringAssert.Contains(markdown, "Mean Gen 1 collections");
            StringAssert.Contains(markdown, "Mean Gen 2 collections");
            StringAssert.Contains(markdown, "offline-imap-search-sort-100k");
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
