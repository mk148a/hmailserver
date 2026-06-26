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

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerAliasAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<AliasAdministrationSnapshot>> GetAliasesAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
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
}
