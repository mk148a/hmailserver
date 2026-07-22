using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("E773E8FC-1C9A-4E96-A73C-CC02E7649637")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceBackupManager
{
    [DispId(1)]
    void StartBackup();

    [DispId(2)]
    IInterfaceBackup LoadBackup([MarshalAs(UnmanagedType.BStr)] string xmlFile);
}

[ComVisible(true)]
[Guid("1BBE5234-D331-41DF-85D7-CAF0B00B3BF7")]
[ProgId("hMailServer.BackupManager.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceBackupManager))]
public sealed class BackupManager : IInterfaceBackupManager
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IBackupArchiveMetadataReader? _metadataReader;
    private readonly IBackupOperationRuntime? _operationRuntime;

    public BackupManager()
    {
    }

    private BackupManager(
        IBackupArchiveMetadataReader metadataReader,
        IBackupOperationRuntime? operationRuntime)
    {
        _metadataReader = metadataReader;
        _operationRuntime = operationRuntime;
    }

    public void StartBackup()
    {
        EnsureAuthorized();
        if (_operationRuntime is null)
        {
            throw NotImplemented();
        }

        SetStatus("Backup started");
        var result = _operationRuntime.TryStartBackup(CreateBackupTask);
        if (result == BackupStartDispatchResult.AlreadyRunning)
        {
            OnBackupFailed("Backup or restore operation is already started");
        }
        else if (result == BackupStartDispatchResult.QueueUnavailable)
        {
            OnBackupFailed("Backup operation failed because random work queue did not exist.");
        }
    }

    public IInterfaceBackup LoadBackup(string xmlFile)
    {
        EnsureAuthorized();
        var containsOptions = _metadataReader!.ReadContainsOptions(xmlFile);
        return Backup.CreateAuthorized(containsOptions);
    }

    internal static BackupManager CreateAuthorized(
        IBackupArchiveMetadataReader? metadataReader = null,
        IBackupOperationRuntime? operationRuntime = null) =>
        new(
            metadataReader ?? SevenZipBackupArchiveMetadataReader.CreateDefault(),
            operationRuntime ?? BackupManagerRuntimeHost.Runtime);

    internal void OnThreadStopped() => _operationRuntime?.OnThreadStopped();

    internal string GetStatus()
    {
        lock (_statusGate)
        {
            return _statusLog;
        }
    }

    internal static BackupStartPlan CreateStartPlan(
        string destination,
        int backupOptions,
        bool backupMessagesDbOnly,
        bool allMessageFilesInDataFolder,
        bool destinationExists) =>
        BackupStartPlan.Evaluate(
            destination,
            backupOptions,
            backupMessagesDbOnly,
            allMessageFilesInDataFolder,
            destinationExists);

    private void EnsureAuthorized()
    {
        if (_metadataReader is null)
        {
            throw new COMException(
                "BackupManager access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private readonly object _statusGate = new();
    private string _statusLog = string.Empty;

    private BackupTaskRequest CreateBackupTask() => new(
        ExecuteBackupAsync,
        SetStatus,
        OnBackupFailed,
        OnBackupCompleted,
        OnThreadStopped);

    private static ValueTask ExecuteBackupAsync(CancellationToken _)
    {
        throw NotImplemented();
    }

    private void SetStatus(string status)
    {
        lock (_statusGate)
        {
            _statusLog += status + "\r\n";
        }
    }

    private void OnBackupFailed(string reason) => SetStatus("BACKUP ERROR: " + reason);

    private void OnBackupCompleted() => SetStatus("Backup completed successfully");

    private static COMException NotImplemented() => new(
        "This BackupManager member is not implemented by the .NET 10 rewrite yet.",
        ENotImplemented);
}

[ComVisible(false)]
internal interface IBackupArchiveMetadataReader
{
    int ReadContainsOptions(string archivePath);
}

[ComVisible(false)]
internal sealed class SevenZipBackupArchiveMetadataReader : IBackupArchiveMetadataReader
{
    internal const string MetadataEntryName = "hMailServerBackup.xml";
    private const long MaximumMetadataCharacters = 512L * 1024 * 1024;

    private readonly string _sevenZipExecutablePath;

    internal SevenZipBackupArchiveMetadataReader(string sevenZipExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sevenZipExecutablePath);
        _sevenZipExecutablePath = sevenZipExecutablePath;
    }

    internal static SevenZipBackupArchiveMetadataReader CreateDefault() =>
        new(Path.Combine(AppContext.BaseDirectory, "7za.exe"));

    public int ReadContainsOptions(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        var fullArchivePath = Path.GetFullPath(archivePath);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(_sevenZipExecutablePath, fullArchivePath)
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("The legacy 7z reader could not be started.");
        }

        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            var options = ParseContainsOptions(process.StandardOutput.BaseStream);
            process.WaitForExit();
            var error = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode is not 0 and not 1)
            {
                throw new InvalidDataException(
                    $"The legacy 7z reader failed with exit code {process.ExitCode}: {error.Trim()}");
            }

            return options;
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.WaitForExit();
            _ = errorTask.GetAwaiter().GetResult();
            throw;
        }
    }

    internal static ProcessStartInfo CreateStartInfo(string executablePath, string archivePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("x");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add(MetadataEntryName);
        startInfo.ArgumentList.Add("-so");
        startInfo.ArgumentList.Add("-y");
        return startInfo;
    }

    internal static int ParseContainsOptions(Stream metadataStream)
    {
        ArgumentNullException.ThrowIfNull(metadataStream);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumMetadataCharacters,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };

        var rootFound = false;
        var options = 0;
        using var reader = XmlReader.Create(metadataStream, settings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Depth == 0)
            {
                rootFound = reader.Name == "Backup";
                continue;
            }

            if (rootFound && reader.Depth == 1 && reader.Name == "BackupInformation")
            {
                _ = int.TryParse(
                    reader.GetAttribute("Mode"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out options);
            }
        }

        return rootFound ? options : 0;
    }
}
