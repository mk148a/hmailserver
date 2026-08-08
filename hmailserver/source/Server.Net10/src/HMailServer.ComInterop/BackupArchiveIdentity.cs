using System.Security.Cryptography;

namespace HMailServer.ComInterop;

[System.Runtime.InteropServices.ComVisible(false)]
internal sealed record BackupArchiveIdentity(string Sha256)
{
    internal static BackupArchiveIdentity? TryCapture(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        var fullPath = Path.GetFullPath(archivePath);
        if (!File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            return null;
        }

        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.SequentialScan);
        return new(Convert.ToHexString(SHA256.HashData(stream)));
    }

    internal bool Matches(string archivePath)
    {
        var current = TryCapture(archivePath);
        return current is not null
            && string.Equals(Sha256, current.Sha256, StringComparison.Ordinal);
    }

    internal static FileStream OpenReadLock(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        return new FileStream(
            Path.GetFullPath(archivePath),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.SequentialScan);
    }
}
