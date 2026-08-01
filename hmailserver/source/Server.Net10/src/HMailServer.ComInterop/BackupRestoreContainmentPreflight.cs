using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal static class BackupRestoreContainmentPreflight
{
    private const int MaximumScannedEntries = 100_000;

    internal static BackupRestoreContainmentPlan Plan(
        BackupRestoreIntegrityEvidence evidence,
        string targetDataDirectoryPath,
        string rollbackArtifactPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDataDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rollbackArtifactPath);

        string archivePath;
        string targetPath;
        string rollbackPath;
        try
        {
            archivePath = Path.GetFullPath(evidence.ArchivePath);
            targetPath = Path.GetFullPath(targetDataDirectoryPath);
            rollbackPath = Path.GetFullPath(rollbackArtifactPath);
        }
        catch (ArgumentException)
        {
            return new BackupRestoreContainmentPlan(
                IsSafe: false,
                FailureReason: "The restore preflight received an invalid path.",
                ArchivePath: evidence.ArchivePath,
                SourcePath: evidence.RawDataBackupPath,
                TargetDataDirectoryPath: targetDataDirectoryPath,
                RollbackArtifactPath: rollbackArtifactPath,
                RequiresIsolatedExtraction: string.Equals(
                    evidence.DataFilesFormat,
                    "7z",
                    StringComparison.OrdinalIgnoreCase)
                    && !evidence.BackupMessagesDbOnly);
        }

        var isRaw = string.Equals(evidence.DataFilesFormat, "Raw", StringComparison.OrdinalIgnoreCase);
        string? sourcePath = null;
        try
        {
            if (isRaw && !string.IsNullOrWhiteSpace(evidence.RawDataBackupPath))
            {
                sourcePath = Path.GetFullPath(evidence.RawDataBackupPath);
            }
        }
        catch (ArgumentException)
        {
            return new BackupRestoreContainmentPlan(
                IsSafe: false,
                FailureReason: "The restore preflight received an invalid raw source path.",
                ArchivePath: archivePath,
                SourcePath: evidence.RawDataBackupPath,
                TargetDataDirectoryPath: targetPath,
                RollbackArtifactPath: rollbackPath,
                RequiresIsolatedExtraction: false);
        }

        var requiresRawSource = isRaw && !evidence.BackupMessagesDbOnly;
        var requiresIsolatedExtraction = string.Equals(
            evidence.DataFilesFormat,
            "7z",
            StringComparison.OrdinalIgnoreCase)
            && !evidence.BackupMessagesDbOnly;

        string? failureReason = null;
        if (!evidence.IsValid)
        {
            failureReason = evidence.FailureReason ?? "The restore evidence is invalid.";
        }
        else if (requiresRawSource && sourcePath is null)
        {
            failureReason = "The raw restore source directory is missing.";
        }
        else if (requiresRawSource
            && sourcePath is not null
            && !IsAccessibleDirectory(sourcePath))
        {
            failureReason = "The raw restore source is not an accessible directory.";
        }
        else if (!File.Exists(archivePath))
        {
            failureReason = "The restore archive file does not exist.";
        }
        else if (File.Exists(targetPath))
        {
            failureReason = "The restore target data path is a file.";
        }
        else if (!Directory.Exists(targetPath))
        {
            failureReason = "The restore target data directory does not exist.";
        }
        else if (IsFileSystemRoot(targetPath) || IsFileSystemRoot(rollbackPath))
        {
            failureReason = "The restore target or rollback path cannot be a filesystem root.";
        }
        else if (Path.GetDirectoryName(rollbackPath) is not string rollbackDirectory
            || !Directory.Exists(rollbackDirectory))
        {
            failureReason = "The rollback artifact parent directory does not exist.";
        }
        else if (File.Exists(rollbackPath) || Directory.Exists(rollbackPath))
        {
            failureReason = "The rollback artifact path already exists.";
        }
        else if (PathsOverlap(archivePath, targetPath)
            || (sourcePath is not null && PathsOverlap(archivePath, sourcePath))
            || (sourcePath is not null && PathsOverlap(sourcePath, targetPath))
            || PathsOverlap(rollbackPath, targetPath)
            || PathsOverlap(rollbackPath, archivePath)
            || (sourcePath is not null && PathsOverlap(rollbackPath, sourcePath)))
        {
            failureReason = "The restore source, target, archive, and rollback paths overlap.";
        }
        else
        {
            var scannedEntries = 0;
            var scanResult = ScanTree(
                archivePath,
                cancellationToken,
                ref scannedEntries);
            if (scanResult == TreeScanResult.Clear && sourcePath is not null)
            {
                scanResult = ScanTree(sourcePath, cancellationToken, ref scannedEntries);
            }

            if (scanResult == TreeScanResult.Clear)
            {
                scanResult = ScanTree(targetPath, cancellationToken, ref scannedEntries);
            }

            if (scanResult == TreeScanResult.Clear && HasReparsePoint(rollbackPath))
            {
                scanResult = TreeScanResult.ReparsePoint;
            }

            failureReason = scanResult switch
            {
                TreeScanResult.Clear => null,
                TreeScanResult.Canceled => "The restore preflight traversal was canceled.",
                TreeScanResult.LimitExceeded => "The restore preflight traversal exceeded its entry limit.",
                _ => "The restore preflight path traverses a reparse point or inaccessible path."
            };
        }

        return new BackupRestoreContainmentPlan(
            IsSafe: failureReason is null,
            FailureReason: failureReason,
            ArchivePath: archivePath,
            SourcePath: sourcePath,
            TargetDataDirectoryPath: targetPath,
            RollbackArtifactPath: rollbackPath,
            RequiresIsolatedExtraction: requiresIsolatedExtraction);
    }

    internal static BackupRestoreContainmentPlan Revalidate(
        BackupRestoreContainmentPlan previousPlan,
        BackupRestoreIntegrityEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previousPlan);
        ArgumentNullException.ThrowIfNull(evidence);

        var currentPlan = Plan(
            evidence,
            previousPlan.TargetDataDirectoryPath,
            previousPlan.RollbackArtifactPath,
            cancellationToken);
        if (!string.Equals(currentPlan.ArchivePath, previousPlan.ArchivePath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(currentPlan.SourcePath, previousPlan.SourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return currentPlan with
            {
                IsSafe = false,
                FailureReason = "The restore preflight paths changed after the initial plan."
            };
        }

        return currentPlan;
    }

    private static bool PathsOverlap(string firstPath, string secondPath)
    {
        var first = NormalizeForComparison(firstPath);
        var second = NormalizeForComparison(secondPath);
        return string.Equals(first, second, StringComparison.OrdinalIgnoreCase)
            || IsWithin(first, second)
            || IsWithin(second, first);
    }

    private static bool IsWithin(string parentPath, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(parentPath, candidatePath);
        return relativePath != "."
            && !Path.IsPathRooted(relativePath)
            && !string.Equals(relativePath, "..", StringComparison.Ordinal)
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string NormalizeForComparison(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool HasReparsePoint(string path)
    {
        var currentPath = Path.GetFullPath(path);
        while (!string.IsNullOrWhiteSpace(currentPath))
        {
            try
            {
                if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }

            var parentPath = Path.GetDirectoryName(currentPath);
            if (string.Equals(parentPath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            currentPath = parentPath!;
        }

        return false;
    }

    private static bool IsAccessibleDirectory(string path)
    {
        try
        {
            return Directory.Exists(path)
                && (File.GetAttributes(path) & FileAttributes.Directory) != 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static TreeScanResult ScanTree(
        string path,
        CancellationToken cancellationToken,
        ref int scannedEntries)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return TreeScanResult.Canceled;
        }

        if (++scannedEntries > MaximumScannedEntries)
        {
            return TreeScanResult.LimitExceeded;
        }

        if (HasReparsePoint(path))
        {
            return TreeScanResult.ReparsePoint;
        }

        if (!Directory.Exists(path))
        {
            return TreeScanResult.Clear;
        }

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return TreeScanResult.Canceled;
                }

                if (++scannedEntries > MaximumScannedEntries)
                {
                    return TreeScanResult.LimitExceeded;
                }

                if (HasReparsePoint(entry))
                {
                    return TreeScanResult.ReparsePoint;
                }

                if (Directory.Exists(entry))
                {
                    var childResult = ScanTree(entry, cancellationToken, ref scannedEntries);
                    if (childResult != TreeScanResult.Clear)
                    {
                        return childResult;
                    }
                }
            }
        }
        catch (IOException)
        {
            return TreeScanResult.Inaccessible;
        }
        catch (UnauthorizedAccessException)
        {
            return TreeScanResult.Inaccessible;
        }

        return TreeScanResult.Clear;
    }

    private enum TreeScanResult
    {
        Clear,
        ReparsePoint,
        Inaccessible,
        Canceled,
        LimitExceeded
    }

    private static bool IsFileSystemRoot(string path) =>
        string.Equals(
            NormalizeForComparison(path),
            NormalizeForComparison(Path.GetPathRoot(path)!),
            StringComparison.OrdinalIgnoreCase);
}

[ComVisible(false)]
internal sealed record BackupRestoreContainmentPlan(
    bool IsSafe,
    string? FailureReason,
    string ArchivePath,
    string? SourcePath,
    string TargetDataDirectoryPath,
    string RollbackArtifactPath,
    bool RequiresIsolatedExtraction);
