using HMailServer.Core.Abstractions;

namespace HMailServer.Service;

public sealed class DatabaseVersionStartupGuard
{
    public const int RuntimeRequiredDatabaseVersion = 6000;

    private readonly IDatabaseAdministrationStore _databaseAdministrationStore;

    public DatabaseVersionStartupGuard(IDatabaseAdministrationStore databaseAdministrationStore)
    {
        ArgumentNullException.ThrowIfNull(databaseAdministrationStore);
        _databaseAdministrationStore = databaseAdministrationStore;
    }

    public async ValueTask EnsureCompatibleAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _databaseAdministrationStore
            .GetDatabaseAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!snapshot.IsConnected)
        {
            throw new InvalidOperationException(
                "The hMailServer database connection could not be established; server startup is refused.");
        }

        if (snapshot.CurrentVersion is not { } currentVersion)
        {
            throw new InvalidOperationException(
                "The hMailServer database version could not be read; server startup is refused.");
        }

        if (currentVersion != RuntimeRequiredDatabaseVersion)
        {
            throw new InvalidOperationException(
                $"The hMailServer runtime database version {currentVersion} does not match the required " +
                $"version {RuntimeRequiredDatabaseVersion}; server startup is refused.");
        }
    }
}
