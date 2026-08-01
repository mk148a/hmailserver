using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupRestoreContainmentPreflightTests
{
    [TestMethod]
    public void Plan_CompressedEvidenceRequiresIsolatedExtraction()
    {
        using var fixture = new TemporaryPaths();

        var plan = BackupRestoreContainmentPreflight.Plan(
            CreateEvidence("7z", rawDataBackupPath: null, fixture.ArchivePath),
            fixture.TargetPath,
            fixture.RollbackPath);

        Assert.IsTrue(plan.IsSafe, plan.FailureReason);
        Assert.IsTrue(plan.RequiresIsolatedExtraction);
        Assert.IsNull(plan.SourcePath);
    }

    [TestMethod]
    public void Plan_RejectsInvalidEvidenceWithoutMutation()
    {
        using var fixture = new TemporaryPaths();
        var evidence = CreateEvidence("7z", rawDataBackupPath: null, fixture.ArchivePath) with
        {
            IsValid = false,
            FailureReason = "invalid evidence"
        };

        var plan = BackupRestoreContainmentPreflight.Plan(
            evidence,
            fixture.TargetPath,
            fixture.RollbackPath);

        Assert.IsFalse(plan.IsSafe);
        Assert.AreEqual("invalid evidence", plan.FailureReason);
        Assert.IsFalse(File.Exists(fixture.RollbackPath));
        Assert.IsTrue(Directory.Exists(fixture.TargetPath));
    }

    [TestMethod]
    public void Plan_RejectsRawSourceAndTargetOverlap()
    {
        using var fixture = new TemporaryPaths(createSource: true);
        var evidence = CreateEvidence("Raw", fixture.SourcePath, fixture.ArchivePath);

        var plan = BackupRestoreContainmentPreflight.Plan(
            evidence,
            fixture.SourcePath,
            fixture.RollbackPath);

        Assert.IsFalse(plan.IsSafe);
        StringAssert.Contains(plan.FailureReason!, "overlap");
    }

    [TestMethod]
    public void Plan_RejectsArchiveInsideRawSource()
    {
        using var fixture = new TemporaryPaths(createSource: true);
        var archivePath = Path.Combine(fixture.SourcePath, "restore.7z");
        File.WriteAllText(archivePath, "archive");

        var plan = BackupRestoreContainmentPreflight.Plan(
            CreateEvidence("Raw", fixture.SourcePath, archivePath),
            fixture.TargetPath,
            fixture.RollbackPath);

        Assert.IsFalse(plan.IsSafe);
        StringAssert.Contains(plan.FailureReason!, "overlap");
    }

    [TestMethod]
    public void Plan_RejectsRollbackCollisionAndContainment()
    {
        using var fixture = new TemporaryPaths(createSource: true);
        var collision = BackupRestoreContainmentPreflight.Plan(
            CreateEvidence("Raw", fixture.SourcePath, fixture.ArchivePath),
            fixture.TargetPath,
            fixture.ExistingRollbackPath);

        Assert.IsFalse(collision.IsSafe);
        StringAssert.Contains(collision.FailureReason!, "already exists");

        var contained = BackupRestoreContainmentPreflight.Plan(
            CreateEvidence("Raw", fixture.SourcePath, fixture.ArchivePath),
            fixture.TargetPath,
            Path.Combine(fixture.TargetPath, "rollback.zip"));

        Assert.IsFalse(contained.IsSafe);
        StringAssert.Contains(contained.FailureReason!, "overlap");
    }

    [TestMethod]
    public void Plan_RejectsNestedReparsePointWhenSupported()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = new TemporaryPaths(createSource: true);
        var realTarget = Path.Combine(fixture.RootPath, "real-target");
        Directory.CreateDirectory(realTarget);
        var link = Path.Combine(fixture.RootPath, "target-link");
        try
        {
            Directory.CreateSymbolicLink(link, realTarget);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(link, "nested"));
        var plan = BackupRestoreContainmentPreflight.Plan(
            CreateEvidence("Raw", fixture.SourcePath, fixture.ArchivePath),
            Path.Combine(link, "nested"),
            fixture.RollbackPath);

        Assert.IsFalse(plan.IsSafe);
        StringAssert.Contains(plan.FailureReason!, "reparse");
    }

    private static BackupRestoreIntegrityEvidence CreateEvidence(
        string format,
        string? rawDataBackupPath,
        string archivePath) =>
        new(archivePath)
        {
            IsValid = true,
            ArchiveTestPassed = true,
            MetadataPresent = true,
            MetadataXmlValid = true,
            BackupOptions = 6,
            DataFilesFormat = format,
            RawDataBackupPath = rawDataBackupPath
        };

    [TestMethod]
    public void Plan_AllowsDbOnlyRawEvidenceWithoutPhysicalSource()
    {
        using var fixture = new TemporaryPaths();
        var plan = BackupRestoreContainmentPreflight.Plan(
            CreateEvidence("Raw", rawDataBackupPath: null, fixture.ArchivePath) with
            {
                BackupMessagesDbOnly = true
            },
            fixture.TargetPath,
            fixture.RollbackPath);

        Assert.IsTrue(plan.IsSafe, plan.FailureReason);
        Assert.IsNull(plan.SourcePath);
        Assert.IsFalse(plan.RequiresIsolatedExtraction);
    }

    [TestMethod]
    public void Plan_CancelsTreeTraversalWithoutMutation()
    {
        using var fixture = new TemporaryPaths();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var plan = BackupRestoreContainmentPreflight.Plan(
            CreateEvidence("7z", rawDataBackupPath: null, fixture.ArchivePath),
            fixture.TargetPath,
            fixture.RollbackPath,
            cancellation.Token);

        Assert.IsFalse(plan.IsSafe);
        StringAssert.Contains(plan.FailureReason!, "canceled");
        Assert.IsTrue(Directory.Exists(fixture.TargetPath));
        Assert.IsFalse(File.Exists(fixture.RollbackPath));
    }

    private sealed class TemporaryPaths : IDisposable
    {
        internal TemporaryPaths(bool createSource = false)
        {
            RootPath = Path.Combine(Path.GetTempPath(), "hmail-restore-preflight-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
            ArchivePath = Path.Combine(RootPath, "restore.7z");
            File.WriteAllText(ArchivePath, "archive");
            TargetPath = Path.Combine(RootPath, "target-data");
            SourcePath = Path.Combine(RootPath, "source-data");
            RollbackPath = Path.Combine(RootPath, "rollback", "state.zip");
            ExistingRollbackPath = Path.Combine(RootPath, "existing.zip");
            Directory.CreateDirectory(TargetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(RollbackPath)!);
            if (createSource)
            {
                Directory.CreateDirectory(SourcePath);
            }

            File.WriteAllText(ExistingRollbackPath, "existing");
        }

        internal string RootPath { get; }
        internal string ArchivePath { get; }
        internal string TargetPath { get; }
        internal string SourcePath { get; }
        internal string RollbackPath { get; }
        internal string ExistingRollbackPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
