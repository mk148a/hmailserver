using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerBackupRestoreMetadataTransactionFactory
    : IBackupRestoreMetadataTransactionFactory
{
    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerBackupRestoreMetadataTransactionFactory(
        SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IBackupRestoreMetadataTransaction> BeginAsync(
        CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = (SqlTransaction)await connection
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            return new SqlServerBackupRestoreMetadataTransaction(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}

internal sealed class SqlServerBackupRestoreMetadataTransaction
    : IBackupRestoreMetadataTransaction
{
    private readonly SqlConnection _connection;
    private readonly SqlTransaction _transaction;
    private readonly SqlServerDomainAdministrationStore _domainStore;
    private bool _commitStarted;
    private bool _committed;

    internal SqlServerBackupRestoreMetadataTransaction(
        SqlConnection connection,
        SqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
        var context = new SqlServerBackupRestoreTransactionContext(connection, transaction);
        _domainStore = new SqlServerDomainAdministrationStore(context);
        DomainStore = _domainStore;
        AccountStore = new SqlServerAccountAdministrationStore(context);
        AliasStore = new SqlServerAliasAdministrationStore(context);
        DistributionListStore = new SqlServerDistributionListAdministrationStore(context);
        RecipientStore = new SqlServerDistributionListRecipientAdministrationStore(context);
    }

    public IDomainAdministrationStore DomainStore { get; }

    public IAccountAdministrationStore AccountStore { get; }

    public IAliasAdministrationStore AliasStore { get; }

    public IDistributionListAdministrationStore DistributionListStore { get; }

    public IDistributionListRecipientAdministrationStore RecipientStore { get; }

    public ValueTask DeleteAllDomainsForRestoreAsync(CancellationToken cancellationToken) =>
        _domainStore.DeleteAllDomainsForRestoreAsync(cancellationToken);

    public async ValueTask CommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _commitStarted = true;
        await _transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_committed && !_commitStarted)
            {
                await _transaction.RollbackAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}

internal sealed class SqlServerBackupRestoreTransactionContext
{
    internal SqlServerBackupRestoreTransactionContext(
        SqlConnection connection,
        SqlTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    internal SqlConnection Connection { get; }

    internal SqlTransaction Transaction { get; }
}

internal sealed class SqlServerCommandLease : IAsyncDisposable
{
    private readonly SqlConnection? _connection;

    private SqlServerCommandLease(SqlCommand command, SqlConnection? connection)
    {
        Command = command;
        _connection = connection;
    }

    internal SqlCommand Command { get; }

    internal static async ValueTask<SqlServerCommandLease> OpenAsync(
        SqlServerConnectionFactory connectionFactory,
        SqlServerBackupRestoreTransactionContext? transactionContext,
        string sql,
        CancellationToken cancellationToken)
    {
        if (transactionContext is not null)
        {
            return new SqlServerCommandLease(
                new SqlCommand(sql, transactionContext.Connection, transactionContext.Transaction),
                connection: null);
        }

        var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new SqlServerCommandLease(new SqlCommand(sql, connection), connection);
    }

    public async ValueTask DisposeAsync()
    {
        Command.Dispose();
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
