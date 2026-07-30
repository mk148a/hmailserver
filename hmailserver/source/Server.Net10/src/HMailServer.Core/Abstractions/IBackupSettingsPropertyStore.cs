namespace HMailServer.Core.Abstractions;

public interface IBackupSettingsPropertyStore
{
    ValueTask<IReadOnlyList<BackupSettingsPropertySnapshot>> GetBackupSettingsPropertiesAsync(
        CancellationToken cancellationToken);
}
