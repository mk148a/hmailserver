using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerAliasAdministrationStore : IAliasAdministrationStore
{
    public const string GetAliasesSql = """
SELECT
    aliasid,
    aliasdomainid,
    aliasname,
    aliasvalue,
    aliasactive
FROM hm_aliases
WHERE aliasdomainid = @DomainID
ORDER BY aliasname ASC;
""";

    public const string InsertAliasSql = """
INSERT INTO hm_aliases
    (aliasdomainid, aliasname, aliasvalue, aliasactive)
OUTPUT INSERTED.aliasid
    VALUES
    (@DomainID, @Name, @Value, @Active);
""";

    public const string UpdateAliasSql = """
UPDATE hm_aliases
SET aliasdomainid = @DomainID,
    aliasname = @Name,
    aliasvalue = @Value,
    aliasactive = @Active
WHERE aliasdomainid = @OwningDomainID
  AND aliasid = @AliasID;
""";

    public const string DeleteAliasSql = """
DELETE FROM hm_aliases
WHERE aliasdomainid = @OwningDomainID
  AND aliasid = @AliasID;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerAliasAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerAliasAdministrationStore(
        SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _connectionFactory = null!;
        _transactionContext = transactionContext;
    }

    private SqlServerConnectionFactory GetStandaloneConnectionFactory() =>
        _transactionContext is not null
            ? throw new InvalidOperationException(
                "Non-transaction-aware operations are not supported on transaction-scoped SQL administration stores.")
            : _connectionFactory;

    public async ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetAliasesSql, connection);
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var aliases = new List<AliasAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            aliases.Add(
                new AliasAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    DomainId: reader.GetInt32(1),
                    Name: reader.GetString(2),
                    Value: reader.GetString(3),
                    Active: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0));
        }

        return aliases;
    }

    public async ValueTask<int> InsertAliasAsync(
        int owningDomainId,
        AliasAdministrationSnapshot alias,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alias);
        await using var commandLease = await SqlServerCommandLease
            .OpenAsync(_connectionFactory, _transactionContext, InsertAliasSql, cancellationToken)
            .ConfigureAwait(false);
        var command = commandLease.Command;
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = owningDomainId;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = alias.Name;
        command.Parameters.Add("@Value", SqlDbType.NVarChar, 255).Value = alias.Value;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = alias.Active ? 1 : 0;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }

    public async ValueTask UpdateAliasAsync(
        int owningDomainId,
        AliasAdministrationSnapshot alias,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        ArgumentNullException.ThrowIfNull(alias);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateAliasSql, connection);
        command.Parameters.Add("@OwningDomainID", SqlDbType.Int).Value = owningDomainId;
        command.Parameters.Add("@AliasID", SqlDbType.Int).Value = alias.Id;
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = owningDomainId;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = alias.Name;
        command.Parameters.Add("@Value", SqlDbType.NVarChar, 255).Value = alias.Value;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = alias.Active ? 1 : 0;
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Updating alias {alias.Id} for owning domain {owningDomainId} affected {affectedRows} rows instead of exactly one.");
        }
    }

    public async ValueTask<bool> DeleteAliasAsync(
        int owningDomainId,
        int aliasId,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteAliasSql, connection);
        command.Parameters.Add("@OwningDomainID", SqlDbType.Int).Value = owningDomainId;
        command.Parameters.Add("@AliasID", SqlDbType.Int).Value = aliasId;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }
}
