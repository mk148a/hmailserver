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
