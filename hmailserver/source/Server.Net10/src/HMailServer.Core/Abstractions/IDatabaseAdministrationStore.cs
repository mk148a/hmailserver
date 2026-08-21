namespace HMailServer.Core.Abstractions;

public interface IDatabaseAdministrationStore
{
    ValueTask<DatabaseAdministrationSnapshot> GetDatabaseAsync(CancellationToken cancellationToken);
}

public interface IDatabaseAdministrationMutationStore : IDatabaseAdministrationStore
{
    ValueTask<IDatabaseAdministrationTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken);
}

public interface IDatabaseAdministrationTransaction : IAsyncDisposable
{
    ValueTask CommitAsync(CancellationToken cancellationToken);

    ValueTask RollbackAsync(CancellationToken cancellationToken);
}
