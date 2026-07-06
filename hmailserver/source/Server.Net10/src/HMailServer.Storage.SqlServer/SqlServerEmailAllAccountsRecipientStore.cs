using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerEmailAllAccountsRecipientStore
    : IEmailAllAccountsRecipientStore
{
    public const string GetAccountsSql = """
SELECT
    accountid,
    accountaddress,
    accountactive
FROM hm_accounts
ORDER BY accountaddress ASC;
""";

    public const string GetDomainsSql = """
SELECT
    domainname,
    domainactive
FROM hm_domains
ORDER BY domainname ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerEmailAllAccountsRecipientStore(
        SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<EmailAllAccountsRecipient>> GetActiveRecipientsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        var accounts = await ReadAccountsAsync(connection, cancellationToken).ConfigureAwait(false);
        var domains = await ReadDomainsAsync(connection, cancellationToken).ConfigureAwait(false);

        var recipients = new List<EmailAllAccountsRecipient>();
        foreach (var account in accounts)
        {
            if (!account.Active)
            {
                continue;
            }

            var domainName = ExtractDomain(account.Address);
            if (!domains.TryGetValue(domainName, out var domainActive) || !domainActive)
            {
                continue;
            }

            recipients.Add(new EmailAllAccountsRecipient(account.Id, account.Address));
        }

        return recipients;
    }

    private static async ValueTask<IReadOnlyList<AccountRow>> ReadAccountsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(GetAccountsSql, connection);
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);
        var accounts = new List<AccountRow>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(
                new AccountRow(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) != 0));
        }

        return accounts;
    }

    private static async ValueTask<IReadOnlyDictionary<string, bool>> ReadDomainsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(GetDomainsSql, connection);
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);
        var domains = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            domains.TryAdd(
                reader.GetString(0),
                Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != 0);
        }

        return domains;
    }

    private static string ExtractDomain(string address)
    {
        var atSign = address.LastIndexOf('@');
        return address[(atSign + 1)..];
    }

    private sealed record AccountRow(int Id, string Address, bool Active);
}
