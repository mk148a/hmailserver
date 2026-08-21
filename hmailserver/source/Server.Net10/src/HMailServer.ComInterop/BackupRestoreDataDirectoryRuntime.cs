using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal sealed class BackupRestoreDataDirectoryRuntime
{
    private readonly string _sevenZipExecutablePath;
    private readonly Action<string, string, CancellationToken> _copyTree;
    private readonly Action<string>? _flushJournalDirectory;
    private readonly IBackupRestoreDataDirectoryMutation _filesystemMutation;

    internal BackupRestoreDataDirectoryRuntime(
        string sevenZipExecutablePath,
        Action<string, string, CancellationToken>? copyTree = null,
        Action<string>? flushJournalDirectory = null,
        IBackupRestoreDataDirectoryMutation? filesystemMutation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sevenZipExecutablePath);
        _sevenZipExecutablePath = sevenZipExecutablePath;
        _copyTree = copyTree ?? WindowsHandleRelativeDirectoryCopier.Copy;
        _flushJournalDirectory = flushJournalDirectory;
        _filesystemMutation = filesystemMutation ?? new WindowsBackupRestoreDataDirectoryMutation();
    }

    internal async ValueTask RestoreAsync(
        BackupRestoreIntegrityEvidence evidence,
        BackupRestoreContainmentPlan plan,
        CancellationToken cancellationToken,
        Func<CancellationToken, ValueTask>? commitAsync = null,
        bool commitOutcomeMayBeAmbiguous = true)
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

        var journalPath = BackupRestoreRecoveryJournal.GetJournalPath(targetPath);
        var pendingRecovery = BackupRestoreRecoveryJournal.InspectPendingRecovery(targetPath);
        if (pendingRecovery.IsPending)
        {
            throw new InvalidOperationException(
                pendingRecovery.FailureReason ?? "A pending restore recovery requires manual review.");
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
            var manifest = new BackupRestoreRecoveryManifest(
                targetPath,
                rollbackPath,
                Path.GetFullPath(evidence.ArchivePath),
                BackupRestoreRecoveryPhase.Prepared);
            BackupRestoreRecoveryJournal.Persist(journalPath, manifest, _flushJournalDirectory);

            var metadataCommitInvoked = false;
            var metadataCommitCompleted = false;
            var rollbackArtifactDeleted = false;
            _filesystemMutation.MoveDirectory(targetPath, rollbackPath);
            try
            {
                WindowsHandleRelativeDirectoryCopier.EnsureDirectory(targetPath);
                _copyTree(sourcePath, targetPath, cancellationToken);
                manifest = manifest with { Phase = BackupRestoreRecoveryPhase.FilesystemSwapped };
                BackupRestoreRecoveryJournal.Persist(journalPath, manifest, _flushJournalDirectory);
                if (commitAsync is not null)
                {
                    manifest = manifest with { Phase = BackupRestoreRecoveryPhase.MetadataCommitStarted };
                    BackupRestoreRecoveryJournal.Persist(journalPath, manifest, _flushJournalDirectory);
                    metadataCommitInvoked = true;
                    await commitAsync(cancellationToken).ConfigureAwait(false);
                    metadataCommitCompleted = true;
                    manifest = manifest with { Phase = BackupRestoreRecoveryPhase.MetadataCommitCompleted };
                    BackupRestoreRecoveryJournal.Persist(journalPath, manifest, _flushJournalDirectory);
                }

                Directory.Delete(rollbackPath, recursive: true);
                rollbackArtifactDeleted = true;
                BackupRestoreRecoveryJournal.Remove(journalPath, _flushJournalDirectory);
            }
            catch (Exception mutationFailure)
            {
                if (rollbackArtifactDeleted
                    || metadataCommitCompleted
                    || (metadataCommitInvoked && commitOutcomeMayBeAmbiguous))
                {
                    throw new InvalidOperationException(
                        rollbackArtifactDeleted
                            ? "The restore rollback artifact was deleted before journal finalization completed; manual recovery is required."
                            : "The restore metadata commit outcome is ambiguous; manual recovery is required.",
                        mutationFailure);
                }

                try
                {
                    manifest = manifest with { Phase = BackupRestoreRecoveryPhase.RollbackStarted };
                    BackupRestoreRecoveryJournal.Persist(journalPath, manifest, _flushJournalDirectory);
                    if (Directory.Exists(targetPath))
                    {
                        Directory.Delete(targetPath, recursive: true);
                    }

                    _filesystemMutation.MoveDirectory(rollbackPath, targetPath);
                    BackupRestoreRecoveryJournal.Remove(journalPath);
                }
                catch (Exception rollbackFailure)
                {
                    try
                    {
                        manifest = manifest with { Phase = BackupRestoreRecoveryPhase.RollbackFailed };
                        BackupRestoreRecoveryJournal.Persist(journalPath, manifest, _flushJournalDirectory);
                    }
                    catch
                    {
                        // Preserve the existing journal when the failure-phase update is unavailable.
                    }

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
