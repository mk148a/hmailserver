using System.Diagnostics;
using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class BackupRestoreDataDirectoryRuntimeTests
{
    [TestMethod]
    public async Task RestoreAsync_RawSiblingReplacesDisposableTargetAndCleansRollback()
    {
        using var fixture = new DataDirectoryFixture();
        Directory.CreateDirectory(Path.Combine(fixture.SourcePath, "example.test"));
        File.WriteAllText(Path.Combine(fixture.SourcePath, "example.test", "new.eml"), "new");
        File.WriteAllText(Path.Combine(fixture.TargetPath, "old.eml"), "old");
        File.WriteAllText(fixture.ArchivePath, "archive");

        var evidence = new BackupRestoreIntegrityEvidence(fixture.ArchivePath)
        {
            IsValid = true,
            ArchiveTestPassed = true,
            MetadataPresent = true,
            MetadataXmlValid = true,
            MessageFilesValidated = true,
            DataFilesFormat = "Raw",
            RawDataBackupPath = fixture.SourcePath,
            BackupMessagesDbOnly = false
        };
        var plan = BackupRestoreContainmentPreflight.Plan(
            evidence,
            fixture.TargetPath,
            fixture.RollbackPath);
        var runtime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"));

        await runtime.RestoreAsync(evidence, plan, CancellationToken.None);

        Assert.IsFalse(File.Exists(Path.Combine(fixture.TargetPath, "old.eml")));
        Assert.AreEqual("new", File.ReadAllText(Path.Combine(fixture.TargetPath, "example.test", "new.eml")));
        Assert.IsFalse(Directory.Exists(fixture.RollbackPath));
        Assert.IsFalse(File.Exists(BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath)));
    }

    [TestMethod]
    public async Task RestoreAsync_CompressedArchiveStagesDataBackupAndCleansExtraction()
    {
        using var fixture = new DataDirectoryFixture();
        var source = Path.Combine(fixture.RootPath, "archive-source");
        Directory.CreateDirectory(Path.Combine(source, "DataBackup", "example.test"));
        File.WriteAllText(Path.Combine(source, "DataBackup", "example.test", "new.eml"), "new");
        File.WriteAllText(Path.Combine(fixture.TargetPath, "old.eml"), "old");
        await CreateArchiveAsync(fixture.ArchivePath, source);

        var evidence = new BackupRestoreIntegrityEvidence(fixture.ArchivePath)
        {
            IsValid = true,
            ArchiveTestPassed = true,
            MetadataPresent = true,
            MetadataXmlValid = true,
            MessageFilesValidated = true,
            DataFilesFormat = "7z",
            BackupMessagesDbOnly = false
        };
        var plan = BackupRestoreContainmentPreflight.Plan(
            evidence,
            fixture.TargetPath,
            fixture.RollbackPath);
        var runtime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"));

        await runtime.RestoreAsync(evidence, plan, CancellationToken.None);

        Assert.IsFalse(File.Exists(Path.Combine(fixture.TargetPath, "old.eml")));
        Assert.AreEqual("new", File.ReadAllText(Path.Combine(fixture.TargetPath, "example.test", "new.eml")));
        Assert.IsFalse(Directory.Exists(fixture.RollbackPath));
        Assert.IsFalse(File.Exists(BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath)));
    }

    [TestMethod]
    public void RecoveryJournal_PersistsBoundedAbsoluteManifestAndPhaseTransitions()
    {
        using var fixture = new DataDirectoryFixture();
        var journalPath = BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath);
        var manifest = new BackupRestoreRecoveryManifest(
            Path.GetFullPath(fixture.TargetPath),
            Path.GetFullPath(fixture.RollbackPath),
            Path.GetFullPath(fixture.ArchivePath),
            BackupRestoreRecoveryPhase.Prepared);

        foreach (var phase in Enum.GetValues<BackupRestoreRecoveryPhase>())
        {
            BackupRestoreRecoveryJournal.Persist(journalPath, manifest with { Phase = phase });

            var pending = BackupRestoreRecoveryJournal.InspectPendingRecovery(fixture.TargetPath);
            Assert.IsTrue(pending.IsPending);
            Assert.IsTrue(pending.RequiresManualRecovery);
            Assert.IsNotNull(pending.Manifest);
            Assert.AreEqual(phase, pending.Manifest!.Phase);
        }

        Assert.IsTrue(new FileInfo(journalPath).Length <= 16 * 1024);
        var journalText = File.ReadAllText(journalPath);
        StringAssert.Contains(journalText, "TargetPath");
        StringAssert.Contains(journalText, "RollbackPath");
        StringAssert.Contains(journalText, "ArchivePath");
    }

    [TestMethod]
    public void RecoveryJournal_FinalizationFailureLeavesReadablePendingJournal()
    {
        using var fixture = new DataDirectoryFixture();
        var journalPath = BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath);
        var manifest = new BackupRestoreRecoveryManifest(
            Path.GetFullPath(fixture.TargetPath),
            Path.GetFullPath(fixture.RollbackPath),
            Path.GetFullPath(fixture.ArchivePath),
            BackupRestoreRecoveryPhase.Prepared);

        Assert.ThrowsExactly<IOException>(
            () => BackupRestoreRecoveryJournal.Persist(
                journalPath,
                manifest,
                _ => throw new IOException("simulated directory flush failure")));

        var pending = BackupRestoreRecoveryJournal.InspectPendingRecovery(fixture.TargetPath);
        Assert.IsTrue(pending.IsPending);
        Assert.IsNotNull(pending.Manifest);
        Assert.AreEqual(BackupRestoreRecoveryPhase.Prepared, pending.Manifest!.Phase);
        Assert.IsTrue(File.Exists(journalPath));
    }

    [TestMethod]
    public void RecoveryJournal_RemoveFinalizationFailurePreservesReadablePendingJournal()
    {
        using var fixture = new DataDirectoryFixture();
        var journalPath = BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath);
        var manifest = new BackupRestoreRecoveryManifest(
            Path.GetFullPath(fixture.TargetPath),
            Path.GetFullPath(fixture.RollbackPath),
            Path.GetFullPath(fixture.ArchivePath),
            BackupRestoreRecoveryPhase.FilesystemSwapped);
        BackupRestoreRecoveryJournal.Persist(journalPath, manifest);

        var flushCount = 0;
        Assert.ThrowsExactly<IOException>(
            () => BackupRestoreRecoveryJournal.Remove(
                journalPath,
                _ =>
                {
                    flushCount++;
                    if (flushCount == 1)
                    {
                        throw new IOException("simulated directory flush failure");
                    }
                }));

        var pending = BackupRestoreRecoveryJournal.InspectPendingRecovery(fixture.TargetPath);
        Assert.IsTrue(pending.IsPending);
        Assert.IsNotNull(pending.Manifest);
        Assert.AreEqual(BackupRestoreRecoveryPhase.FilesystemSwapped, pending.Manifest!.Phase);
        Assert.IsTrue(File.Exists(journalPath));
        Assert.AreEqual(2, flushCount);
    }

    [TestMethod]
    public void RecoveryJournal_DetectsInterruptedMetadataCommitWithoutMutating()
    {
        using var fixture = new DataDirectoryFixture();
        var journalPath = BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath);
        BackupRestoreRecoveryJournal.Persist(
            journalPath,
            new BackupRestoreRecoveryManifest(
                Path.GetFullPath(fixture.TargetPath),
                Path.GetFullPath(fixture.RollbackPath),
                Path.GetFullPath(fixture.ArchivePath),
                BackupRestoreRecoveryPhase.MetadataCommitStarted));

        var pending = BackupRestoreRecoveryJournal.InspectPendingRecovery(fixture.TargetPath);

        Assert.IsTrue(pending.IsPending);
        Assert.IsTrue(pending.RequiresManualRecovery);
        Assert.IsNotNull(pending.FailureReason);
        Assert.IsTrue(File.Exists(journalPath));
        Assert.IsTrue(Directory.Exists(fixture.TargetPath));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => BackupRestoreRecoveryJournal.EnsureNoPendingRecovery(fixture.TargetPath));
    }

    [TestMethod]
    public async Task RestoreAsync_RefusesPendingRecoveryBeforeMovingTarget()
    {
        using var fixture = new DataDirectoryFixture();
        File.WriteAllText(Path.Combine(fixture.SourcePath, "new.eml"), "new");
        File.WriteAllText(Path.Combine(fixture.TargetPath, "old.eml"), "old");
        File.WriteAllText(fixture.ArchivePath, "archive");
        var evidence = CreateRawEvidence(fixture);
        var plan = BackupRestoreContainmentPreflight.Plan(
            evidence,
            fixture.TargetPath,
            fixture.RollbackPath);
        BackupRestoreRecoveryJournal.Persist(
            BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath),
            new BackupRestoreRecoveryManifest(
                Path.GetFullPath(fixture.TargetPath),
                Path.GetFullPath(fixture.RollbackPath),
                Path.GetFullPath(fixture.ArchivePath),
                BackupRestoreRecoveryPhase.FilesystemSwapped));
        var runtime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => runtime.RestoreAsync(evidence, plan, CancellationToken.None).AsTask());

        Assert.AreEqual("old", File.ReadAllText(Path.Combine(fixture.TargetPath, "old.eml")));
        Assert.IsFalse(Directory.Exists(fixture.RollbackPath));
    }

    [TestMethod]
    public async Task RestoreAsync_RejectsDbOnlyEvidenceWithoutMovingTarget()
    {
        using var fixture = new DataDirectoryFixture();
        File.WriteAllText(Path.Combine(fixture.TargetPath, "old.eml"), "old");
        File.WriteAllText(fixture.ArchivePath, "archive");
        var evidence = new BackupRestoreIntegrityEvidence(fixture.ArchivePath)
        {
            IsValid = true,
            DataFilesFormat = "Raw",
            RawDataBackupPath = fixture.SourcePath,
            BackupMessagesDbOnly = true
        };
        var plan = BackupRestoreContainmentPreflight.Plan(
            evidence,
            fixture.TargetPath,
            fixture.RollbackPath);
        var runtime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => runtime.RestoreAsync(evidence, plan, CancellationToken.None).AsTask());

        Assert.AreEqual("old", File.ReadAllText(Path.Combine(fixture.TargetPath, "old.eml")));
        Assert.IsFalse(Directory.Exists(fixture.RollbackPath));
    }

    [TestMethod]
    public async Task RestoreAsync_RecordsMetadataCommitPhaseBeforeCallbackAndCleansJournal()
    {
        using var fixture = new DataDirectoryFixture();
        File.WriteAllText(Path.Combine(fixture.SourcePath, "new.eml"), "new");
        File.WriteAllText(Path.Combine(fixture.TargetPath, "old.eml"), "old");
        File.WriteAllText(fixture.ArchivePath, "archive");
        var evidence = CreateRawEvidence(fixture);
        var plan = BackupRestoreContainmentPreflight.Plan(
            evidence,
            fixture.TargetPath,
            fixture.RollbackPath);
        var sawMetadataCommitStarted = false;
        var runtime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"));

        await runtime.RestoreAsync(
            evidence,
            plan,
            CancellationToken.None,
            commitAsync: _ =>
            {
                var pending = BackupRestoreRecoveryJournal.InspectPendingRecovery(fixture.TargetPath);
                Assert.IsNotNull(pending.Manifest);
                Assert.AreEqual(
                    BackupRestoreRecoveryPhase.MetadataCommitStarted,
                    pending.Manifest!.Phase);
                sawMetadataCommitStarted = true;
                return default;
            },
            commitOutcomeMayBeAmbiguous: false);

        Assert.IsTrue(sawMetadataCommitStarted);
        Assert.IsFalse(File.Exists(BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath)));
        Assert.IsFalse(Directory.Exists(fixture.RollbackPath));
    }

    [TestMethod]
    public async Task RestoreAsync_PreservesEvidenceAfterAmbiguousMetadataCommit()
    {
        using var fixture = new DataDirectoryFixture();
        File.WriteAllText(Path.Combine(fixture.SourcePath, "new.eml"), "new");
        File.WriteAllText(Path.Combine(fixture.TargetPath, "old.eml"), "old");
        File.WriteAllText(fixture.ArchivePath, "archive");
        var evidence = CreateRawEvidence(fixture);
        var plan = BackupRestoreContainmentPreflight.Plan(
            evidence,
            fixture.TargetPath,
            fixture.RollbackPath);
        var runtime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => runtime.RestoreAsync(
                evidence,
                plan,
                CancellationToken.None,
                commitAsync: _ => throw new IOException("ambiguous metadata commit"))
                .AsTask());

        var pending = BackupRestoreRecoveryJournal.InspectPendingRecovery(fixture.TargetPath);
        Assert.IsTrue(pending.IsPending);
        Assert.IsNotNull(pending.Manifest);
        Assert.AreEqual(BackupRestoreRecoveryPhase.MetadataCommitStarted, pending.Manifest!.Phase);
        Assert.AreEqual("old", File.ReadAllText(Path.Combine(fixture.RollbackPath, "old.eml")));
        Assert.IsTrue(File.Exists(Path.Combine(fixture.TargetPath, "new.eml")));
    }

    [TestMethod]
    public async Task RestoreAsync_PreservesNewTargetWhenFinalJournalFlushFailsAfterRollbackDeletion()
    {
        using var fixture = new DataDirectoryFixture();
        File.WriteAllText(Path.Combine(fixture.SourcePath, "new.eml"), "new");
        File.WriteAllText(Path.Combine(fixture.TargetPath, "old.eml"), "old");
        File.WriteAllText(fixture.ArchivePath, "archive");
        var evidence = CreateRawEvidence(fixture);
        var plan = BackupRestoreContainmentPreflight.Plan(evidence, fixture.TargetPath, fixture.RollbackPath);
        var flushCount = 0;
        var runtime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            flushJournalDirectory: _ =>
            {
                flushCount++;
                if (flushCount == 3)
                {
                    throw new IOException("simulated final journal flush failure");
                }
            });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => runtime.RestoreAsync(evidence, plan, CancellationToken.None).AsTask());

        Assert.AreEqual(4, flushCount);
        Assert.IsTrue(File.Exists(Path.Combine(fixture.TargetPath, "new.eml")));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.TargetPath, "old.eml")));
        Assert.IsFalse(Directory.Exists(fixture.RollbackPath));
        Assert.IsTrue(File.Exists(BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath)));
    }

    [TestMethod]
    public async Task RestoreAsync_RestoresOriginalTargetWhenStagingFails()
    {
        using var fixture = new DataDirectoryFixture();
        File.WriteAllText(Path.Combine(fixture.SourcePath, "new.eml"), "new");
        File.WriteAllText(Path.Combine(fixture.TargetPath, "old.eml"), "old");
        File.WriteAllText(fixture.ArchivePath, "archive");
        var evidence = new BackupRestoreIntegrityEvidence(fixture.ArchivePath)
        {
            IsValid = true,
            ArchiveTestPassed = true,
            MetadataPresent = true,
            MetadataXmlValid = true,
            MessageFilesValidated = true,
            DataFilesFormat = "Raw",
            RawDataBackupPath = fixture.SourcePath,
            BackupMessagesDbOnly = false
        };
        var plan = BackupRestoreContainmentPreflight.Plan(
            evidence,
            fixture.TargetPath,
            fixture.RollbackPath);
        var runtime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            (_, _, _) => throw new IOException("simulated staging failure"));

        await Assert.ThrowsExactlyAsync<IOException>(
            () => runtime.RestoreAsync(evidence, plan, CancellationToken.None).AsTask());

        Assert.AreEqual("old", File.ReadAllText(Path.Combine(fixture.TargetPath, "old.eml")));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.TargetPath, "new.eml")));
        Assert.IsFalse(Directory.Exists(fixture.RollbackPath));
        Assert.IsFalse(File.Exists(BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath)));
    }

    [TestMethod]
    public async Task RestoreAsync_PreservesJournalWhenRollbackFails()
    {
        using var fixture = new DataDirectoryFixture();
        File.WriteAllText(Path.Combine(fixture.SourcePath, "new.eml"), "new");
        File.WriteAllText(Path.Combine(fixture.TargetPath, "old.eml"), "old");
        File.WriteAllText(fixture.ArchivePath, "archive");
        var evidence = CreateRawEvidence(fixture);
        var plan = BackupRestoreContainmentPreflight.Plan(
            evidence,
            fixture.TargetPath,
            fixture.RollbackPath);
        var runtime = new BackupRestoreDataDirectoryRuntime(
            Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            (_, _, _) =>
            {
                Directory.Delete(fixture.RollbackPath, recursive: true);
                throw new IOException("simulated staging failure");
            });

        await Assert.ThrowsExactlyAsync<AggregateException>(
            () => runtime.RestoreAsync(evidence, plan, CancellationToken.None).AsTask());

        var pending = BackupRestoreRecoveryJournal.InspectPendingRecovery(fixture.TargetPath);
        Assert.IsTrue(pending.IsPending);
        Assert.IsNotNull(pending.Manifest);
        Assert.AreEqual(BackupRestoreRecoveryPhase.RollbackFailed, pending.Manifest!.Phase);
        Assert.IsTrue(File.Exists(BackupRestoreRecoveryJournal.GetJournalPath(fixture.TargetPath)));
        Assert.IsFalse(Directory.Exists(fixture.RollbackPath));
    }

    [TestMethod]
    public void EnsureSafeDataBackupRoot_RejectsNonDirectoryRoots()
    {
        using var fixture = new DataDirectoryFixture();
        File.WriteAllText(fixture.ArchivePath, "not-a-directory");

        Assert.ThrowsExactly<InvalidDataException>(
            () => BackupRestoreDataDirectoryRuntime.EnsureSafeDataBackupRoot(fixture.ArchivePath));
    }

    [TestMethod]
    public void Boundary_DisposePreservesRollbackArtifact()
    {
        using var fixture = new DataDirectoryFixture();
        Directory.CreateDirectory(fixture.RollbackPath);
        File.WriteAllText(Path.Combine(fixture.RollbackPath, "original.txt"), "original");

        using (new BackupRestoreDataDirectoryBoundary(fixture.TargetPath, fixture.RollbackPath))
        {
        }

        Assert.IsTrue(File.Exists(Path.Combine(fixture.RollbackPath, "original.txt")));
    }

    private static async Task CreateArchiveAsync(string archivePath, string sourcePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(AppContext.BaseDirectory, "7za.exe"),
            WorkingDirectory = sourcePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("a");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add("DataBackup");
        startInfo.ArgumentList.Add("-t7z");
        startInfo.ArgumentList.Add("-mx1");
        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        _ = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.AreEqual(0, process.ExitCode, error);
    }

    private static BackupRestoreIntegrityEvidence CreateRawEvidence(DataDirectoryFixture fixture) =>
        new(fixture.ArchivePath)
        {
            IsValid = true,
            ArchiveTestPassed = true,
            MetadataPresent = true,
            MetadataXmlValid = true,
            MessageFilesValidated = true,
            DataFilesFormat = "Raw",
            RawDataBackupPath = fixture.SourcePath,
            BackupMessagesDbOnly = false
        };

    private sealed class DataDirectoryFixture : IDisposable
    {
        public DataDirectoryFixture()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"hmailserver-data-restore-{Guid.NewGuid():N}");
            SourcePath = Path.Combine(RootPath, "DataBackup");
            TargetPath = Path.Combine(RootPath, "Data");
            ArchivePath = Path.Combine(RootPath, "backup.7z");
            RollbackPath = Path.Combine(RootPath, "rollback");
            Directory.CreateDirectory(SourcePath);
            Directory.CreateDirectory(TargetPath);
        }

        public string RootPath { get; }
        public string SourcePath { get; }
        public string TargetPath { get; }
        public string ArchivePath { get; }
        public string RollbackPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
