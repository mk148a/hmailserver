using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal enum BackupRestoreRecoveryPhase
{
    Prepared,
    FilesystemSwapped,
    MetadataCommitStarted,
    MetadataCommitCompleted,
    RollbackStarted,
    RollbackFailed
}

[ComVisible(false)]
internal sealed record BackupRestoreRecoveryManifest(
    string TargetPath,
    string RollbackPath,
    string ArchivePath,
    BackupRestoreRecoveryPhase Phase);

[ComVisible(false)]
internal sealed record BackupRestorePendingRecovery(
    bool IsPending,
    bool RequiresManualRecovery,
    string? FailureReason,
    BackupRestoreRecoveryManifest? Manifest);

[ComVisible(false)]
public static class BackupRestoreRecoveryJournal
{
    private const int MaximumManifestBytes = 16 * 1024;
    private const int MaximumPathLength = 4096;
    private const string JournalFileName = ".hmailserver-restore-recovery.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    internal static string GetJournalPath(string targetDataDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDataDirectoryPath);
        var targetPath = Path.GetFullPath(targetDataDirectoryPath);
        var parentPath = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException("The restore target has no parent directory.", nameof(targetDataDirectoryPath));
        return Path.Combine(parentPath, JournalFileName);
    }

    public static void EnsureNoPendingRecovery(string targetDataDirectoryPath)
    {
        var pendingRecovery = InspectPendingRecovery(targetDataDirectoryPath);
        if (pendingRecovery.IsPending)
        {
            throw new InvalidOperationException(
                pendingRecovery.FailureReason
                    ?? "An interrupted restore requires manual recovery before service startup.");
        }
    }

    internal static void Persist(string journalPath, BackupRestoreRecoveryManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateManifest(manifest);

        var fullJournalPath = Path.GetFullPath(journalPath);
        var payload = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        if (payload.Length > MaximumManifestBytes)
        {
            throw new InvalidDataException("The restore recovery manifest exceeds its bounded size.");
        }

        var temporaryPath = fullJournalPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       options: FileOptions.WriteThrough))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullJournalPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    internal static BackupRestorePendingRecovery InspectPendingRecovery(string targetDataDirectoryPath)
    {
        string journalPath;
        try
        {
            journalPath = GetJournalPath(targetDataDirectoryPath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            return new BackupRestorePendingRecovery(
                IsPending: true,
                RequiresManualRecovery: true,
                FailureReason: "The restore recovery journal path is invalid.",
                Manifest: null);
        }

        try
        {
            using var stream = new FileStream(
                journalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.SequentialScan);
            var fileLength = stream.Length;
            if (fileLength <= 0 || fileLength > MaximumManifestBytes)
            {
                return InvalidRecovery("The restore recovery journal is missing or exceeds its bounded size.");
            }

            var payload = new byte[(int)fileLength];
            stream.ReadExactly(payload);
            var manifest = JsonSerializer.Deserialize<BackupRestoreRecoveryManifest>(payload, JsonOptions);
            if (manifest is null)
            {
                return InvalidRecovery("The restore recovery journal is empty.");
            }

            ValidateManifest(manifest);
            var targetPath = Path.GetFullPath(targetDataDirectoryPath);
            if (!string.Equals(manifest.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return InvalidRecovery("The restore recovery journal target does not match the inspected target.");
            }

            return new BackupRestorePendingRecovery(
                IsPending: true,
                RequiresManualRecovery: true,
                FailureReason: "An interrupted or incomplete restore requires manual recovery review.",
                Manifest: manifest);
        }
        catch (FileNotFoundException)
        {
            return new BackupRestorePendingRecovery(
                IsPending: false,
                RequiresManualRecovery: false,
                FailureReason: null,
                Manifest: null);
        }
        catch (DirectoryNotFoundException)
        {
            return new BackupRestorePendingRecovery(
                IsPending: false,
                RequiresManualRecovery: false,
                FailureReason: null,
                Manifest: null);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException
            or ArgumentException)
        {
            return InvalidRecovery("The restore recovery journal could not be read safely.");
        }
    }

    internal static void Remove(string journalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        File.Delete(Path.GetFullPath(journalPath));
    }

    private static BackupRestorePendingRecovery InvalidRecovery(string reason) =>
        new(
            IsPending: true,
            RequiresManualRecovery: true,
            FailureReason: reason,
            Manifest: null);

    private static void ValidateManifest(BackupRestoreRecoveryManifest manifest)
    {
        ValidateAbsolutePath(manifest.TargetPath, nameof(manifest.TargetPath));
        ValidateAbsolutePath(manifest.RollbackPath, nameof(manifest.RollbackPath));
        ValidateAbsolutePath(manifest.ArchivePath, nameof(manifest.ArchivePath));
        if (!Enum.IsDefined(manifest.Phase))
        {
            throw new InvalidDataException("The restore recovery journal contains an unknown phase.");
        }
    }

    private static void ValidateAbsolutePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.Length > MaximumPathLength
            || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidDataException($"The restore recovery journal contains an invalid {parameterName}.");
        }
    }
}
