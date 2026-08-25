using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDomainAliasAdministrationStore : IDomainAliasAdministrationStore
{
    public const string GetDomainAliasesSql = """
SELECT
    daid,
    dadomainid,
    daalias
FROM hm_domain_aliases
WHERE dadomainid = @DomainID
ORDER BY daid ASC;
""";

    public const string InsertDomainAliasSql = """
INSERT INTO hm_domain_aliases
    (dadomainid, daalias)
OUTPUT INSERTED.daid
VALUES
    (@DomainID, @AliasName);
""";

    public const string UpdateDomainAliasSql = """
UPDATE hm_domain_aliases
SET dadomainid = @DomainID,
    daalias = @AliasName
WHERE dadomainid = @OwningDomainID
  AND daid = @AliasID;
""";

    public const string DeleteDomainAliasSql = """
DELETE FROM hm_domain_aliases
WHERE dadomainid = @OwningDomainID
  AND daid = @AliasID;
""";

    private readonly SqlServerConnectionFactory? _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    private SqlServerConnectionFactory ConnectionFactory =>
        _connectionFactory ?? throw new NotSupportedException(
            "Domain-alias mutation is not supported from a backup snapshot scope.");

    public SqlServerDomainAliasAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerDomainAliasAdministrationStore(
        SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _transactionContext = transactionContext;
    }

    public async ValueTask<IReadOnlyList<DomainAliasAdministrationSnapshot>> GetDomainAliasesAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        await using var lease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            GetDomainAliasesSql,
            cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var aliases = new List<DomainAliasAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            aliases.Add(
                new DomainAliasAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    DomainId: reader.GetInt32(1),
                    AliasName: reader.GetString(2)));
        }

        return aliases;
    }

    public async ValueTask<int> InsertDomainAliasAsync(
        int owningDomainId,
        DomainAliasAdministrationSnapshot alias,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alias);
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertDomainAliasSql, connection);
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = owningDomainId;
        command.Parameters.Add("@AliasName", SqlDbType.NVarChar, 255).Value = alias.AliasName;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask UpdateDomainAliasAsync(
        int owningDomainId,
        DomainAliasAdministrationSnapshot alias,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alias);
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateDomainAliasSql, connection);
        command.Parameters.Add("@OwningDomainID", SqlDbType.Int).Value = owningDomainId;
        command.Parameters.Add("@AliasID", SqlDbType.Int).Value = alias.Id;
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = owningDomainId;
        command.Parameters.Add("@AliasName", SqlDbType.NVarChar, 255).Value = alias.AliasName;
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Updating domain alias {alias.Id} for owning domain {owningDomainId} affected {affectedRows} rows instead of exactly one.");
        }
    }

    public async ValueTask<bool> DeleteDomainAliasAsync(
        int owningDomainId,
        int aliasId,
        CancellationToken cancellationToken)
    {
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteDomainAliasSql, connection);
        command.Parameters.Add("@OwningDomainID", SqlDbType.Int).Value = owningDomainId;
        command.Parameters.Add("@AliasID", SqlDbType.Int).Value = aliasId;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }
}
