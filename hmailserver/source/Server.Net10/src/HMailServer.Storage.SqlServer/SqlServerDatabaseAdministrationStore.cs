using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDatabaseAdministrationStore : IDatabaseAdministrationMutationStore
{
    public const int RequiredDatabaseVersion = 5708;

    public const string CurrentVersionSql = """
SELECT TOP (1) [value]
FROM hm_dbversion;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly LegacyDatabaseConfiguration _configuration;

    public SqlServerDatabaseAdministrationStore(
        SqlServerConnectionFactory connectionFactory,
        LegacyDatabaseConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        _connectionFactory = connectionFactory;
        _configuration = configuration;
    }

    public async ValueTask<DatabaseAdministrationSnapshot> GetDatabaseAsync(
        CancellationToken cancellationToken)
    {
        int? currentVersion = null;
        var isConnected = false;

        await using var connection = await TryOpenAsync(cancellationToken).ConfigureAwait(false);
        if (connection is not null)
        {
            isConnected = true;
            await using var command = new SqlCommand(CurrentVersionSql, connection);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            currentVersion = result is null || result is DBNull
                ? 0
                : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        return new DatabaseAdministrationSnapshot(
            RequiredVersion: RequiredDatabaseVersion,
            CurrentVersion: currentVersion,
            DatabaseType: _configuration.DatabaseType,
            DatabaseExists: _configuration.DatabaseExists,
            IsConnected: isConnected,
            ServerName: _configuration.ServerName,
            DatabaseName: _configuration.DatabaseName);
    }

    public async ValueTask<IDatabaseAdministrationTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            return new SqlServerDatabaseAdministrationTransaction(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<SqlConnection?> TryOpenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private sealed class SqlServerDatabaseAdministrationTransaction(
        SqlConnection connection,
        SqlTransaction transaction) : IDatabaseAdministrationTransaction
    {
        private SqlConnection? _connection = connection;
        private SqlTransaction? _transaction = transaction;

        public async ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            var transactionToCommit = _transaction
                ?? throw new InvalidOperationException("The SQL transaction is no longer active.");
            await transactionToCommit.CommitAsync(cancellationToken).ConfigureAwait(false);
            await DisposeAsync().ConfigureAwait(false);
        }

        public async ValueTask RollbackAsync(CancellationToken cancellationToken)
        {
            var transactionToRollback = _transaction
                ?? throw new InvalidOperationException("The SQL transaction is no longer active.");
            await transactionToRollback.RollbackAsync(cancellationToken).ConfigureAwait(false);
            await DisposeAsync().ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            var transaction = Interlocked.Exchange(ref _transaction, null);
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }

            var connection = Interlocked.Exchange(ref _connection, null);
            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
