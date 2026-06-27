using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerRuleAdministrationStore : IRuleAdministrationStore
{
    public const string GetRulesSql = """
SELECT
    ruleid,
    ruleaccountid,
    rulename,
    ruleactive,
    ruleuseand,
    rulesortorder
FROM hm_rules
WHERE ruleaccountid = @AccountID
ORDER BY rulesortorder ASC, ruleid ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerRuleAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetRulesAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetRulesSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var rules = new List<RuleAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rules.Add(
                new RuleAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    AccountId: reader.GetInt32(1),
                    Name: reader.GetString(2),
                    Active: ReadLegacyBoolean(reader, 3),
                    UseAnd: ReadLegacyBoolean(reader, 4),
                    SortOrder: reader.GetInt32(5)));
        }

        return rules;
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
