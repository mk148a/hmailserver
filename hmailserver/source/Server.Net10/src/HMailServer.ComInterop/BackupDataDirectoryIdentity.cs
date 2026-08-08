using System.Security.Cryptography;
using System.Text;

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

        var before = Capture(fullSourcePath);
        CopyDirectory(fullSourcePath, fullSnapshotPath);
        var after = Capture(fullSourcePath);
        if (!string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal))
        {
            throw new IOException("The raw DataBackup source changed while it was being bound.");
        }

        return Capture(fullSnapshotPath);
    }

    internal bool Matches(string path)
    {
        try
        {
            return string.Equals(Sha256, Capture(path).Sha256, StringComparison.Ordinal);
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

    private static BackupDataDirectoryIdentity Capture(string path)
    {
        var fullPath = Path.GetFullPath(path);
        EnsureSafeDirectory(fullPath);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendDirectory(hash, fullPath, relativePath: string.Empty);
        return new(Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        EnsureSafeDirectory(sourcePath);
        Directory.CreateDirectory(destinationPath);
        RejectReparsePoint(destinationPath);

        foreach (var directory in Directory.EnumerateDirectories(sourcePath).OrderBy(static item => item, StringComparer.OrdinalIgnoreCase))
        {
            RejectReparsePoint(directory);
            CopyDirectory(directory, Path.Combine(destinationPath, Path.GetFileName(directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath).OrderBy(static item => item, StringComparer.OrdinalIgnoreCase))
        {
            RejectReparsePoint(file);
            File.Copy(file, Path.Combine(destinationPath, Path.GetFileName(file)), overwrite: false);
        }
    }

    private static void AppendDirectory(
        IncrementalHash hash,
        string directoryPath,
        string relativePath)
    {
        Append(hash, "D\0" + relativePath);
        foreach (var directory in Directory.EnumerateDirectories(directoryPath).OrderBy(static item => item, StringComparer.OrdinalIgnoreCase))
        {
            RejectReparsePoint(directory);
            AppendDirectory(hash, directory, CombineRelative(relativePath, Path.GetFileName(directory)));
        }

        foreach (var file in Directory.EnumerateFiles(directoryPath).OrderBy(static item => item, StringComparer.OrdinalIgnoreCase))
        {
            RejectReparsePoint(file);
            Append(hash, "F\0" + CombineRelative(relativePath, Path.GetFileName(file)));
            using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan);
            var buffer = new byte[64 * 1024];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, bytesRead);
            }
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

    private static string CombineRelative(string parent, string child) =>
        string.IsNullOrEmpty(parent) ? child : parent + "/" + child;

    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData(new byte[] { 0 });
    }
}
