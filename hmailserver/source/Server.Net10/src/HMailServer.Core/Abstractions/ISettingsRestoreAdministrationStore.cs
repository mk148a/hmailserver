namespace HMailServer.Core.Abstractions;

public interface ISettingsRestoreAdministrationStore
{
    ValueTask RestoreSettingsPropertiesAsync(
        IReadOnlyList<BackupSettingsPropertySnapshot> properties,
        CancellationToken cancellationToken);
}
