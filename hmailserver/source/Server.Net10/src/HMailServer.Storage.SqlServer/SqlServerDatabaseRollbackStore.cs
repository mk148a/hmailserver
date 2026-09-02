using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDatabaseRollbackStore
{
    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly string _databaseName;

    public SqlServerDatabaseRollbackStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
        var connection = new SqlConnectionStringBuilder(connectionFactory.ConnectionString);
        _databaseName = connection.InitialCatalog;
        if (string.IsNullOrWhiteSpace(_databaseName)
            || string.Equals(_databaseName, "master", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "A non-master SQL Server database is required for upgrade rollback protection.");
        }
    }

    public string DatabaseName => _databaseName;

    public async ValueTask CreateCopyOnlyBackupAsync(
        string backupPath,
        CancellationToken cancellationToken)
    {
        var fullBackupPath = ValidateBackupPath(backupPath);
        if (File.Exists(fullBackupPath))
        {
            throw new IOException($"The SQL rollback backup already exists: {fullBackupPath}");
        }

        await using var connection = await OpenMasterAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = $"BACKUP DATABASE {QuoteIdentifier(_databaseName)} "
            + $"TO DISK = N'{QuoteLiteral(fullBackupPath)}' "
            + "WITH COPY_ONLY, INIT, CHECKSUM;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (!File.Exists(fullBackupPath) || new FileInfo(fullBackupPath).Length == 0)
        {
            throw new IOException(
                $"SQL Server reported a successful backup but no local backup artifact was created: {fullBackupPath}");
        }
    }

    public async ValueTask RestoreCopyOnlyBackupAsync(
        string backupPath,
        CancellationToken cancellationToken)
    {
        var fullBackupPath = ValidateBackupPath(backupPath);
        if (!File.Exists(fullBackupPath))
        {
            throw new FileNotFoundException("The SQL rollback backup was not found.", fullBackupPath);
        }

        SqlConnection.ClearAllPools();
        await using var connection = await OpenMasterAsync(cancellationToken).ConfigureAwait(false);
        var database = QuoteIdentifier(_databaseName);
        var backup = QuoteLiteral(fullBackupPath);
        await ExecuteNonQueryAsync(
            connection,
            $"ALTER DATABASE {database} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;",
            cancellationToken).ConfigureAwait(false);

        Exception? restoreException = null;
        try
        {
            await ExecuteNonQueryAsync(
                connection,
                $"RESTORE DATABASE {database} FROM DISK = N'{backup}' WITH REPLACE, RECOVERY;",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            restoreException = exception;
        }

        try
        {
            await ExecuteNonQueryAsync(
                connection,
                $"ALTER DATABASE {database} SET MULTI_USER;",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception multiUserException) when (restoreException is not null)
        {
            throw new AggregateException(
                "SQL Server restore failed and the database could not be returned to MULTI_USER mode.",
                restoreException,
                multiUserException);
        }

        if (restoreException is not null)
        {
            throw new InvalidOperationException("SQL Server database restore failed.", restoreException);
        }
    }

    private async ValueTask<SqlConnection> OpenMasterAsync(CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(_connectionFactory.ConnectionString)
        {
            InitialCatalog = "master"
        };
        var connection = new SqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask ExecuteNonQueryAsync(
        SqlConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ValidateBackupPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("The SQL rollback backup path must include a directory.", nameof(path));
        }

        Directory.CreateDirectory(directory);
        return fullPath;
    }

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string QuoteLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
