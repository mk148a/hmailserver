using System.Security.Cryptography;

namespace HMailServer.ComInterop;

internal sealed class BackupArchiveBinding : IDisposable
{
    private readonly string _snapshotDirectory;
    private readonly FileStream _snapshotReadLock;
    private int _disposed;

    private BackupArchiveBinding(
        string snapshotDirectory,
        string archivePath,
        BackupArchiveIdentity identity,
        FileStream snapshotReadLock,
        BackupDataDirectoryIdentity? rawDataBackupIdentity)
    {
        _snapshotDirectory = snapshotDirectory;
        ArchivePath = archivePath;
        Identity = identity;
        _snapshotReadLock = snapshotReadLock;
        RawDataBackupIdentity = rawDataBackupIdentity;
    }

    internal string ArchivePath { get; }

    internal BackupArchiveIdentity Identity { get; }

    internal BackupDataDirectoryIdentity? RawDataBackupIdentity { get; }

    internal static BackupArchiveBinding? TryCreate(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath) || Directory.Exists(fullSourcePath))
        {
            return null;
        }

        var snapshotDirectory = Path.Combine(
            Path.GetTempPath(),
            "hmailserver-backup-bindings",
            Guid.NewGuid().ToString("N"));
        var snapshotPath = Path.Combine(snapshotDirectory, "archive.7z");

        try
        {
            Directory.CreateDirectory(snapshotDirectory);
            BackupArchiveIdentity identity;
            using (var source = BackupArchiveIdentity.OpenReadLock(fullSourcePath))
            using (var snapshot = new FileStream(
                snapshotPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[64 * 1024];
                int bytesRead;
                while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    snapshot.Write(buffer, 0, bytesRead);
                    hash.AppendData(buffer, 0, bytesRead);
                }

                snapshot.Flush(flushToDisk: true);
                identity = new BackupArchiveIdentity(
                    Convert.ToHexString(hash.GetHashAndReset()));
            }

            var rawSourcePath = Path.Combine(
                Path.GetDirectoryName(fullSourcePath)!,
                "DataBackup");
            BackupDataDirectoryIdentity? rawDataBackupIdentity = null;
            if (Directory.Exists(rawSourcePath))
            {
                rawDataBackupIdentity = BackupDataDirectoryIdentity.CopyStableSnapshot(
                    rawSourcePath,
                    Path.Combine(snapshotDirectory, "DataBackup"));
            }

            var snapshotReadLock = BackupArchiveIdentity.OpenReadLock(snapshotPath);
            return new(
                snapshotDirectory,
                snapshotPath,
                identity,
                snapshotReadLock,
                rawDataBackupIdentity);
        }
        catch
        {
            TryDelete(snapshotDirectory);
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _snapshotReadLock.Dispose();
        TryDelete(_snapshotDirectory);
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; the snapshot is never used after binding failure.
        }
    }
}
