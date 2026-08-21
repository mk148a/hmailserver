using System.Security.Cryptography;
using System.Text.Json;
using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerUpgradeArtifactHandoffTests
{
    [TestMethod]
    public async Task PrepareAsync_EmitsReadyManifestOnlyForCompletedMatchingEvidence()
    {
        using var fixture = new HandoffFixture();
        var checkpoint = fixture.CreateCheckpoint();
        var result = fixture.CreateCompletedResult(checkpoint);

        var handoff = await new SqlServerUpgradeArtifactHandoff().PrepareAsync(
            checkpoint,
            result,
            fixture.UpgradeReportPath,
            fixture.HandoffManifestPath,
            CancellationToken.None);

        Assert.AreEqual(SqlServerUpgradeHandoffStatus.ReadyForServiceMutation, handoff.Status);
        Assert.IsTrue(handoff.Manifest.ServiceMutationAllowed);
        Assert.IsTrue(File.Exists(fixture.HandoffManifestPath));
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(fixture.HandoffManifestPath));
        Assert.AreEqual("ReadyForServiceMutation", manifest.RootElement.GetProperty("status").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(
            manifest.RootElement.GetProperty("upgradeReportSha256").GetString()));
    }

    [TestMethod]
    public async Task PrepareAsync_RefusesFailedUpgradeAndDisallowsMutation()
    {
        using var fixture = new HandoffFixture();
        var checkpoint = fixture.CreateCheckpoint();
        var failed = fixture.CreateCompletedResult(checkpoint) with
        {
            Status = SqlServerUpgradeRunStatus.ReinitializeFailed
        };

        var handoff = await new SqlServerUpgradeArtifactHandoff().PrepareAsync(
            checkpoint,
            failed,
            fixture.UpgradeReportPath,
            fixture.HandoffManifestPath,
            CancellationToken.None);

        Assert.AreEqual(SqlServerUpgradeHandoffStatus.Refused, handoff.Status);
        Assert.IsFalse(handoff.Manifest.ServiceMutationAllowed);
        StringAssert.Contains(handoff.Manifest.RefusalReason, "ReinitializeFailed");
    }

    [TestMethod]
    public async Task PrepareAsync_RefusesChangedBackupArtifact()
    {
        using var fixture = new HandoffFixture();
        var checkpoint = fixture.CreateCheckpoint();
        var result = fixture.CreateCompletedResult(checkpoint);
        await File.AppendAllTextAsync(fixture.BackupPath, "tampered");

        var handoff = await new SqlServerUpgradeArtifactHandoff().PrepareAsync(
            checkpoint,
            result,
            fixture.UpgradeReportPath,
            fixture.HandoffManifestPath,
            CancellationToken.None);

        Assert.AreEqual(SqlServerUpgradeHandoffStatus.Refused, handoff.Status);
        Assert.IsFalse(handoff.Manifest.ServiceMutationAllowed);
        StringAssert.Contains(handoff.Manifest.RefusalReason, "digest");
    }

    [TestMethod]
    public async Task PrepareAsync_RefusesMalformedUpgradeReport()
    {
        using var fixture = new HandoffFixture();
        var checkpoint = fixture.CreateCheckpoint();
        var result = fixture.CreateCompletedResult(checkpoint);
        await File.WriteAllTextAsync(fixture.UpgradeReportPath, "not-json");

        var handoff = await new SqlServerUpgradeArtifactHandoff().PrepareAsync(
            checkpoint,
            result,
            fixture.UpgradeReportPath,
            fixture.HandoffManifestPath,
            CancellationToken.None);

        Assert.AreEqual(SqlServerUpgradeHandoffStatus.Refused, handoff.Status);
        Assert.IsFalse(handoff.Manifest.ServiceMutationAllowed);
        StringAssert.Contains(handoff.Manifest.RefusalReason, "valid JSON");
    }

    private sealed class HandoffFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"hmailserver-net10-handoff-{Guid.NewGuid():N}");

        public HandoffFixture()
        {
            Directory.CreateDirectory(_root);
            BackupPath = Path.Combine(_root, "verified-backup.bak");
            UpgradeReportPath = Path.Combine(_root, "upgrade-report.json");
            HandoffManifestPath = Path.Combine(_root, "handoff.json");
            File.WriteAllText(BackupPath, "verified backup");
            File.WriteAllText(
                UpgradeReportPath,
                "{\"status\":\"Completed\",\"migration\":{\"status\":\"Completed\"}}");
        }

        public string BackupPath { get; }
        public string UpgradeReportPath { get; }
        public string HandoffManifestPath { get; }

        public SqlServerVerifiedBackupCheckpoint CreateCheckpoint()
        {
            var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(BackupPath)));
            return new SqlServerVerifiedBackupCheckpoint(
                BackupPath,
                digest,
                DateTimeOffset.UtcNow,
                "isolated:handoff");
        }

        public SqlServerUpgradeRunResult CreateCompletedResult(
            SqlServerVerifiedBackupCheckpoint checkpoint) =>
            new(
                SqlServerUpgradeRunStatus.Completed,
                checkpoint,
                new SqlServerDatabaseMigrationResult(
                    SqlServerDatabaseMigrationStatus.Completed,
                    5708,
                    6000,
                    1,
                    1,
                    null,
                    []),
                null);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
