namespace HMailServer.Core.Abstractions;

public interface IDatabaseAdministrationStore
{
    ValueTask<DatabaseAdministrationSnapshot> GetDatabaseAsync(CancellationToken cancellationToken);
}
