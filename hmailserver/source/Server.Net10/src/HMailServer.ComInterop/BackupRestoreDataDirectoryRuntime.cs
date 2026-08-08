using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal sealed class BackupRestoreDataDirectoryRuntime
{
    private readonly string _sevenZipExecutablePath;
    private readonly Action<string, string, CancellationToken> _copyTree;

    internal BackupRestoreDataDirectoryRuntime(
        string sevenZipExecutablePath,
        Action<string, string, CancellationToken>? copyTree = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sevenZipExecutablePath);
        _sevenZipExecutablePath = sevenZipExecutablePath;
        _copyTree = copyTree ?? CopyTree;
    }

    internal async ValueTask RestoreAsync(
        BackupRestoreIntegrityEvidence evidence,
        BackupRestoreContainmentPlan plan,
        CancellationToken cancellationToken,
        Func<CancellationToken, ValueTask>? commitAsync = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsSafe || !evidence.IsValid)
        {
            throw new InvalidOperationException(
                plan.FailureReason ?? evidence.FailureReason ?? "The restore data-directory preflight failed.");
        }

        if (evidence.BackupMessagesDbOnly)
        {
            throw new InvalidOperationException("DB-only backups cannot restore a data directory.");
        }

        if (!evidence.ArchiveTestPassed
            || !evidence.MetadataPresent
            || !evidence.MetadataXmlValid
            || !evidence.MessageFilesValidated)
        {
            throw new InvalidDataException("The restore payload has not passed the required integrity evidence.");
        }

        var format = evidence.DataFilesFormat;
        if (!string.Equals(format, "Raw", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(format, "7z", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The restore DataFiles format is not supported.");
        }

        var targetPath = Path.GetFullPath(plan.TargetDataDirectoryPath);
        var rollbackPath = Path.GetFullPath(plan.RollbackArtifactPath);
        if (!Directory.Exists(targetPath)
            || File.Exists(rollbackPath)
            || Directory.Exists(rollbackPath))
        {
            throw new InvalidOperationException("The restore target or rollback artifact is not ready.");
        }

        string? extractionPath = null;
        try
        {
            var sourcePath = string.Equals(format, "Raw", StringComparison.OrdinalIgnoreCase)
                ? RequireRawSource(plan)
                : Path.Combine(
                    extractionPath = await ExtractDataBackupAsync(
                        evidence.ArchivePath,
                        cancellationToken).ConfigureAwait(false),
                    "DataBackup");

            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(targetPath, rollbackPath);
            try
            {
                Directory.CreateDirectory(targetPath);
                _copyTree(sourcePath, targetPath, cancellationToken);
                if (commitAsync is not null)
                {
                    await commitAsync(cancellationToken).ConfigureAwait(false);
                }

                Directory.Delete(rollbackPath, recursive: true);
            }
            catch (Exception mutationFailure)
            {
                try
                {
                    if (Directory.Exists(targetPath))
                    {
                        Directory.Delete(targetPath, recursive: true);
                    }

                    Directory.Move(rollbackPath, targetPath);
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException(
                        "Data-directory restore rollback failed after the mutation failure.",
                        mutationFailure,
                        rollbackFailure);
                }

                throw;
            }
        }
        finally
        {
            if (extractionPath is not null && Directory.Exists(extractionPath))
            {
                Directory.Delete(extractionPath, recursive: true);
            }
        }
    }

    private static string RequireRawSource(BackupRestoreContainmentPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.SourcePath)
            || !Directory.Exists(plan.SourcePath))
        {
            throw new InvalidOperationException("The raw DataBackup source directory is missing.");
        }

        var sourcePath = Path.GetFullPath(plan.SourcePath);
        EnsureSafeDataBackupRoot(sourcePath);
        return sourcePath;
    }

    private async ValueTask<string> ExtractDataBackupAsync(
        string archivePath,
        CancellationToken cancellationToken)
    {
        var extractionRoot = Path.Combine(
            Path.GetTempPath(),
            $"hmailserver-restore-data-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractionRoot);
        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _sevenZipExecutablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("x");
            startInfo.ArgumentList.Add(Path.GetFullPath(archivePath));
            startInfo.ArgumentList.Add("-o" + extractionRoot);
            startInfo.ArgumentList.Add("DataBackup");
            startInfo.ArgumentList.Add("-y");

            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The legacy 7z extractor could not be started.");
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            _ = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidDataException(
                    $"The compressed DataBackup payload could not be extracted: {error.Trim()}");
            }

            var dataBackupPath = Path.Combine(extractionRoot, "DataBackup");
            if (!Directory.Exists(dataBackupPath))
            {
                throw new InvalidDataException("The compressed restore does not contain DataBackup.");
            }

            EnsureSafeDataBackupRoot(dataBackupPath);

            return extractionRoot;
        }
        catch
        {
            if (process is not null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            if (Directory.Exists(extractionRoot))
            {
                Directory.Delete(extractionRoot, recursive: true);
            }

            throw;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static void CopyTree(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourcePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(directory);
            var targetDirectory = Path.Combine(targetPath, Path.GetFileName(directory));
            Directory.CreateDirectory(targetDirectory);
            CopyTree(directory, targetDirectory, cancellationToken);
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(file);
            File.Copy(file, Path.Combine(targetPath, Path.GetFileName(file)), overwrite: false);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("The restore source contains a reparse point.");
        }
    }

    internal static void EnsureSafeDataBackupRoot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Directory.Exists(path))
        {
            throw new InvalidDataException("The restore DataBackup root is not a directory.");
        }

        RejectReparsePointChain(path);
    }

    private static void RejectReparsePointChain(string path)
    {
        var currentPath = Path.GetFullPath(path);
        while (!string.IsNullOrWhiteSpace(currentPath))
        {
            RejectReparsePoint(currentPath);
            var parentPath = Path.GetDirectoryName(currentPath);
            if (string.Equals(parentPath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            currentPath = parentPath!;
        }
    }
}

[ComVisible(false)]
internal sealed class BackupRestoreDataDirectoryBoundary : IDisposable
{
    internal BackupRestoreDataDirectoryBoundary(
        string targetDataDirectoryPath,
        string rollbackArtifactPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDataDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rollbackArtifactPath);

        TargetDataDirectoryPath = targetDataDirectoryPath;
        RollbackArtifactPath = rollbackArtifactPath;
    }

    internal string TargetDataDirectoryPath { get; }

    internal string RollbackArtifactPath { get; }

    public void Dispose()
    {
        // BackupRestoreDataDirectoryRuntime owns cleanup after successful finalization.
        // Preserve a rollback artifact when recovery itself failed.
    }
}
