using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed class BackupStartPlanRuntime
{
    private readonly ISettingsAdministrationStore _settingsStore;
    private readonly IBackupPreflightAdministrationStore _preflightStore;
    private readonly string _dataDirectory;
    private readonly bool _backupMessagesDbOnly;
    private readonly Func<string, bool> _pathExists;
    private readonly IBackupSettingsPropertyStore? _backupSettingsPropertyStore;

    public BackupStartPlanRuntime(
        ISettingsAdministrationStore settingsStore,
        IBackupPreflightAdministrationStore preflightStore,
        string dataDirectory,
        bool backupMessagesDbOnly,
        Func<string, bool>? pathExists = null,
        IBackupSettingsPropertyStore? backupSettingsPropertyStore = null)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(preflightStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        _settingsStore = settingsStore;
        _preflightStore = preflightStore;
        _dataDirectory = dataDirectory;
        _backupMessagesDbOnly = backupMessagesDbOnly;
        _pathExists = pathExists ?? DefaultPathExists;
        _backupSettingsPropertyStore = backupSettingsPropertyStore;
    }

    public async ValueTask<BackupStartPlanEvidence> GetEvidenceAsync(
        CancellationToken cancellationToken)
    {
        var settings = await _settingsStore
            .GetSettingsAsync(cancellationToken)
            .ConfigureAwait(false);
        var normalizedDestination = NormalizeDestination(settings.BackupDestination);
        var allMessageFilesInDataDirectory =
            (settings.BackupOptions & BackupStartPlan.BackupMessagesFlag) == 0
                || await _preflightStore
                    .AreAllMessageFilesInDataDirectoryAsync(_dataDirectory, cancellationToken)
                    .ConfigureAwait(false);
        var backupSettingsProperties =
            (settings.BackupOptions & BackupStartPlan.BackupSettingsFlag) != 0
                && _backupSettingsPropertyStore is not null
            ? await _backupSettingsPropertyStore
                .GetBackupSettingsPropertiesAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

        return new BackupStartPlanEvidence(
            Destination: settings.BackupDestination,
            BackupOptions: settings.BackupOptions,
            BackupMessagesDbOnly: _backupMessagesDbOnly,
            AllMessageFilesInDataDirectory: allMessageFilesInDataDirectory,
            DestinationExists: _pathExists(normalizedDestination),
            Settings: settings,
            BackupSettingsProperties: backupSettingsProperties);
    }

    internal static string NormalizeDestination(string destination) =>
        destination.Length > 0 && destination[^1] == '\\'
            ? destination[..^1]
            : destination;

    private static bool DefaultPathExists(string path) =>
        File.Exists(path) || Directory.Exists(path);
}
