using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerRuleCriteriaAdministrationStore : IRuleCriteriaAdministrationStore
{
    public const string GetRuleCriteriaSql = """
SELECT
    criteriaid,
    criteriaruleid,
    criteriamatchvalue,
    criteriausepredefined,
    criteriapredefinedfield,
    criteriamatchtype,
    criteriaheadername
FROM hm_rule_criterias
WHERE criteriaruleid = @RuleId
ORDER BY criteriaid ASC;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerRuleCriteriaAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<RuleCriteriaAdministrationSnapshot>> GetRuleCriteriaAsync(
        int ruleId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetRuleCriteriaSql, connection);
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = ruleId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var criteria = new List<RuleCriteriaAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            criteria.Add(
                new RuleCriteriaAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    RuleId: reader.GetInt32(1),
                    MatchValue: reader.GetString(2),
                    UsePredefined: ReadLegacyBoolean(reader, 3),
                    PredefinedField: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    MatchType: Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture),
                    HeaderField: reader.GetString(6)));
        }

        return criteria;
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
