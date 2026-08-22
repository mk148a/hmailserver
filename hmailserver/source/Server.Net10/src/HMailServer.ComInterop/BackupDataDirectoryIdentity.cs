namespace HMailServer.ComInterop;

internal sealed record BackupDataDirectoryIdentity(string Sha256)
{
    internal static BackupDataDirectoryIdentity CopyStableSnapshot(
        string sourcePath,
        string snapshotPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        var fullSnapshotPath = Path.GetFullPath(snapshotPath);
        EnsureSafeDirectory(fullSourcePath);
        if (Directory.Exists(fullSnapshotPath) || File.Exists(fullSnapshotPath))
        {
            throw new IOException("The DataBackup snapshot path already exists.");
        }

        var before = WindowsHandleRelativeDirectoryCopier.ComputeSha256(fullSourcePath);
        WindowsHandleRelativeDirectoryCopier.Copy(fullSourcePath, fullSnapshotPath);
        var after = WindowsHandleRelativeDirectoryCopier.ComputeSha256(fullSourcePath);
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            throw new IOException("The raw DataBackup source changed while it was being bound.");
        }

        return new(WindowsHandleRelativeDirectoryCopier.ComputeSha256(fullSnapshotPath));
    }

    internal bool Matches(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            EnsureSafeDirectory(fullPath);
            return string.Equals(
                Sha256,
                WindowsHandleRelativeDirectoryCopier.ComputeSha256(fullPath),
                StringComparison.Ordinal);
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

    private static void EnsureSafeDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new InvalidDataException("The raw DataBackup source is not a directory.");
        }

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

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("The raw DataBackup tree contains a reparse point.");
        }
    }

}
