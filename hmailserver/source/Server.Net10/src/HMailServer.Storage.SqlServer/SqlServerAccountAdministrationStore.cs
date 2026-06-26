using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerAccountAdministrationStore : IAccountAdministrationStore
{
    public const string GetAccountsSql = """
SELECT
    accountid,
    accountdomainid,
    accountaddress,
    accountactive,
    accountadminlevel
FROM hm_accounts
WHERE accountdomainid = @DomainID
ORDER BY accountaddress ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerAccountAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetAccountsSql, connection);
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var accounts = new List<AccountAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(
                new AccountAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    DomainId: reader.GetInt32(1),
                    Address: reader.GetString(2),
                    Active: Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0,
                    AdminLevel: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture)));
        }

        return accounts;
    }
}
