using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
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

    internal static void Persist(
        string journalPath,
        BackupRestoreRecoveryManifest manifest,
        Action<string>? flushDirectory = null)
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
            FlushContainingDirectory(fullJournalPath, flushDirectory);
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

    internal static void Remove(string journalPath, Action<string>? flushDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        var fullJournalPath = Path.GetFullPath(journalPath);
        var journalEvidence = File.Exists(fullJournalPath)
            ? File.ReadAllBytes(fullJournalPath)
            : null;
        try
        {
            File.Delete(fullJournalPath);
            FlushContainingDirectory(fullJournalPath, flushDirectory);
        }
        catch (Exception finalizationFailure) when (finalizationFailure is IOException
            or UnauthorizedAccessException
            or PlatformNotSupportedException
            or ArgumentException
            or NotSupportedException)
        {
            if (journalEvidence is not null && !File.Exists(fullJournalPath))
            {
                try
                {
                    RestoreJournalEvidence(fullJournalPath, journalEvidence, flushDirectory);
                }
                catch (Exception evidenceFailure)
                {
                    throw new AggregateException(
                        "The restore recovery journal could not be finalized or preserved.",
                        finalizationFailure,
                        evidenceFailure);
                }
            }

            throw;
        }
    }

    private static void FlushContainingDirectory(string journalPath, Action<string>? flushDirectory)
    {
        var directoryPath = Path.GetDirectoryName(journalPath)
            ?? throw new IOException("The restore recovery journal has no containing directory.");
        (flushDirectory ?? FlushDirectory)(directoryPath);
    }

    private static void FlushDirectory(string directoryPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            using var directoryHandle = File.OpenHandle(
                directoryPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            RandomAccess.FlushToDisk(directoryHandle);
            return;
        }

        using var windowsDirectoryHandle = CreateFileW(
            directoryPath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (windowsDirectoryHandle.IsInvalid)
        {
            throw CreateWindowsIoException("The restore recovery journal directory could not be opened for durable finalization.");
        }

        if (!FlushFileBuffers(windowsDirectoryHandle))
        {
            throw CreateWindowsIoException("The restore recovery journal directory could not be flushed for durable finalization.");
        }
    }

    private static void RestoreJournalEvidence(
        string journalPath,
        byte[] payload,
        Action<string>? flushDirectory)
    {
        var temporaryPath = journalPath + ".preserve-" + Guid.NewGuid().ToString("N");
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

            File.Move(temporaryPath, journalPath, overwrite: true);
            FlushContainingDirectory(journalPath, flushDirectory);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static IOException CreateWindowsIoException(string message) =>
        new(message, new Win32Exception(Marshal.GetLastWin32Error()));

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushFileBuffers(SafeFileHandle fileHandle);

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
