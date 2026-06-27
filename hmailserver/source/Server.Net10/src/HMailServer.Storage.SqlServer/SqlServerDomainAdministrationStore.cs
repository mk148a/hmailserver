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
    domainactive,
    domainpostmaster,
    domainmaxmessagesize,
    domainuseplusaddressing,
    domainplusaddressingchar,
    domainmaxsize,
    domainmaxnoofaccounts,
    domainmaxnoofaliases,
    domainmaxnoofdistributionlists,
    domainlimitationsenabled,
    domainmaxaccountsize
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
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var active = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) != 0;
            var postmaster = reader.GetString(3);
            var maxMessageSize = reader.GetInt32(4);
            var plusAddressingEnabled = Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) != 0;
            var plusAddressingCharacter = reader.GetString(6);
            var maxSize = reader.GetInt32(7);
            var maxNumberOfAccounts = reader.GetInt32(8);
            var maxNumberOfAliases = reader.GetInt32(9);
            var maxNumberOfDistributionLists = reader.GetInt32(10);
            var limitationsEnabled = Convert.ToInt32(reader.GetValue(11), CultureInfo.InvariantCulture);
            var maxAccountSize = reader.GetInt32(12);
            domains.Add(
                new DomainAdministrationSnapshot(
                    Id: id,
                    Name: name,
                    Active: active,
                    Postmaster: postmaster,
                    MaxMessageSize: maxMessageSize,
                    PlusAddressingEnabled: plusAddressingEnabled,
                    PlusAddressingCharacter: plusAddressingCharacter,
                    MaxSize: maxSize,
                    MaxNumberOfAccounts: maxNumberOfAccounts,
                    MaxNumberOfAliases: maxNumberOfAliases,
                    MaxNumberOfDistributionLists: maxNumberOfDistributionLists,
                    MaxNumberOfAccountsEnabled: (limitationsEnabled & 1) != 0,
                    MaxNumberOfAliasesEnabled: (limitationsEnabled & 2) != 0,
                    MaxNumberOfDistributionListsEnabled: (limitationsEnabled & 4) != 0,
                    MaxAccountSize: maxAccountSize));
        }

        return domains;
    }
}
