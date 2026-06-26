using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDistributionListAdministrationStore : IDistributionListAdministrationStore
{
    public const string GetDistributionListsSql = """
SELECT
    distributionlistid,
    distributionlistdomainid,
    distributionlistaddress,
    distributionlistenabled,
    distributionlistrequireauth,
    distributionlistrequireaddress,
    distributionlistmode
FROM hm_distributionlists
WHERE distributionlistdomainid = @DomainID
ORDER BY distributionlistaddress ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerDistributionListAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetDistributionListsSql, connection);
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var lists = new List<DistributionListAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            lists.Add(
                new DistributionListAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    DomainId: reader.GetInt32(1),
                    Address: reader.GetString(2),
                    Active: Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0,
                    RequireSmtpAuth: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0,
                    RequireSenderAddress: reader.GetString(5),
                    Mode: Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture)));
        }

        return lists;
    }
}
