using System.Text.Json;
using HMailServer.Net10.Benchmarks;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class AclRevalidationBenchmarkTests
{
    [TestMethod]
    public void OfflineBackendRefusesToFabricateLatency()
    {
        var report = AclRevalidationBenchmark.CreateNotRunReport("test", "fixture unavailable");

        Assert.AreEqual("not-run", report.Status);
        Assert.AreEqual("offline", report.Backend);
        Assert.IsNull(report.P95Milliseconds);
        Assert.IsFalse(report.ThresholdPassed);
        Assert.AreEqual(0, report.SamplesMilliseconds.Count);
    }

    [TestMethod]
    public void ArtifactWriterPreservesNotRunStatusAndSafetyMetadata()
    {
        var report = AclRevalidationBenchmark.CreateNotRunReport("test", "fixture unavailable");
        var outputDirectory = Path.Combine(Path.GetTempPath(), "hmailserver-net10-acl-benchmark-" + Guid.NewGuid().ToString("N"));
        try
        {
            AclRevalidationArtifactWriter.Write(report, outputDirectory);

            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "acl-revalidation.json")));
            Assert.AreEqual("not-run", json.RootElement.GetProperty("Status").GetString());
            Assert.AreEqual("net10", json.RootElement.GetProperty("Implementation").GetString());
            StringAssert.Contains(File.ReadAllText(Path.Combine(outputDirectory, "acl-revalidation.csv")), "threshold_passed");
            StringAssert.Contains(File.ReadAllText(Path.Combine(outputDirectory, "acl-revalidation.md")), "not a C++ comparison");
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
