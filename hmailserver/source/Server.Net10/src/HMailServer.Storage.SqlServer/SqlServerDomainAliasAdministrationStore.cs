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

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDomainAliasAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<DomainAliasAdministrationSnapshot>> GetDomainAliasesAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetDomainAliasesSql, connection);
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
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertDomainAliasSql, connection);
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = owningDomainId;
        command.Parameters.Add("@AliasName", SqlDbType.NVarChar, 255).Value = alias.AliasName;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, System.Globalization.CultureInfo.InvariantCulture);
    }
}
