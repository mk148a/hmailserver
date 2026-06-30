namespace HMailServer.Core.Abstractions;

public interface ISettingsAdministrationStore
{
    ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(
        CancellationToken cancellationToken);
}
