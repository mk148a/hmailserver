using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDomainAdministrationStore : IDomainAdministrationStore
{
    public const string GetDomainsSql = """
SELECT
    domainid,
    domainname,
    domainactive
FROM hm_domains
ORDER BY domainname ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDomainAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetDomainsSql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var domains = new List<DomainAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            domains.Add(
                new DomainAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    Name: reader.GetString(1),
                    Active: Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) != 0));
        }

        return domains;
    }
}
