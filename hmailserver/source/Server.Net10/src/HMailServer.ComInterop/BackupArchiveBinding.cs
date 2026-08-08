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
        FileStream snapshotReadLock)
    {
        _snapshotDirectory = snapshotDirectory;
        ArchivePath = archivePath;
        Identity = identity;
        _snapshotReadLock = snapshotReadLock;
    }

    internal string ArchivePath { get; }

    internal BackupArchiveIdentity Identity { get; }

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
            using (var source = BackupArchiveIdentity.OpenReadLock(fullSourcePath))
            using (var snapshot = new FileStream(
                snapshotPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan))
            {
                source.CopyTo(snapshot);
                snapshot.Flush(flushToDisk: true);
            }

            var identity = BackupArchiveIdentity.TryCapture(snapshotPath)
                ?? throw new IOException("The archive snapshot could not be identified.");
            var snapshotReadLock = BackupArchiveIdentity.OpenReadLock(snapshotPath);
            return new(snapshotDirectory, snapshotPath, identity, snapshotReadLock);
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
