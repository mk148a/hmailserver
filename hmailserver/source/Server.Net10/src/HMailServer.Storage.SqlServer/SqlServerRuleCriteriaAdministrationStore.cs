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

    public const string DeleteRuleCriteriaByIdSql = """
DELETE FROM hm_rule_criterias
WHERE criteriaruleid = @RuleId
  AND criteriaid = @CriteriaId;
""";

    public const string InsertRuleCriteriaSql = """
INSERT INTO hm_rule_criterias
    (criteriaruleid, criteriausepredefined, criteriapredefinedfield,
     criteriaheadername, criteriamatchtype, criteriamatchvalue)
OUTPUT INSERTED.criteriaid
VALUES
    (@RuleId, @UsePredefined, @PredefinedField,
     @HeaderField, @MatchType, @MatchValue);
""";

    public const string SaveRuleCriteriaSql = """
UPDATE hm_rule_criterias
SET criteriaruleid = @RuleId,
    criteriausepredefined = @UsePredefined,
    criteriapredefinedfield = @PredefinedField,
    criteriaheadername = @HeaderField,
    criteriamatchtype = @MatchType,
    criteriamatchvalue = @MatchValue
WHERE criteriaruleid = @OwningRuleId
  AND criteriaid = @CriteriaId;
""";

    private readonly SqlServerConnectionFactory? _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerRuleCriteriaAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerRuleCriteriaAdministrationStore(SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _transactionContext = transactionContext;
    }

    public async ValueTask<IReadOnlyList<RuleCriteriaAdministrationSnapshot>> GetRuleCriteriaAsync(
        int ruleId,
        CancellationToken cancellationToken)
    {
        await using var lease = await SqlServerCommandLease.OpenAsync(_connectionFactory, _transactionContext, GetRuleCriteriaSql, cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
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

    public async ValueTask DeleteRuleCriteriaByIdAsync(
        int ruleId,
        int databaseId,
        CancellationToken cancellationToken)
    {
        await using var lease = await SqlServerCommandLease.OpenAsync(_connectionFactory, _transactionContext, DeleteRuleCriteriaByIdSql, cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = ruleId;
        command.Parameters.Add("@CriteriaId", SqlDbType.Int).Value = databaseId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> InsertRuleCriteriaAsync(
        int owningRuleId,
        RuleCriteriaAdministrationSnapshot criterion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(owningRuleId);

        await using var lease = await SqlServerCommandLease.OpenAsync(_connectionFactory, _transactionContext, InsertRuleCriteriaSql, cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = owningRuleId;
        command.Parameters.Add("@UsePredefined", SqlDbType.TinyInt).Value = criterion.UsePredefined ? 1 : 0;
        command.Parameters.Add("@PredefinedField", SqlDbType.TinyInt).Value = criterion.PredefinedField;
        command.Parameters.Add("@HeaderField", SqlDbType.NVarChar, 255).Value = criterion.HeaderField;
        command.Parameters.Add("@MatchType", SqlDbType.TinyInt).Value = criterion.MatchType;
        command.Parameters.Add("@MatchValue", SqlDbType.NVarChar, 255).Value = criterion.MatchValue;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }

    public async ValueTask SaveRuleCriteriaAsync(
        int owningRuleId,
        RuleCriteriaAdministrationSnapshot criterion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        await using var lease = await SqlServerCommandLease.OpenAsync(_connectionFactory, _transactionContext, SaveRuleCriteriaSql, cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@OwningRuleId", SqlDbType.Int).Value = owningRuleId;
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = criterion.RuleId;
        command.Parameters.Add("@CriteriaId", SqlDbType.Int).Value = criterion.Id;
        command.Parameters.Add("@UsePredefined", SqlDbType.TinyInt).Value = criterion.UsePredefined ? 1 : 0;
        command.Parameters.Add("@PredefinedField", SqlDbType.TinyInt).Value = criterion.PredefinedField;
        command.Parameters.Add("@HeaderField", SqlDbType.NVarChar, 255).Value = criterion.HeaderField;
        command.Parameters.Add("@MatchType", SqlDbType.TinyInt).Value = criterion.MatchType;
        command.Parameters.Add("@MatchValue", SqlDbType.NVarChar, 255).Value = criterion.MatchValue;
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Saving rule criterion {criterion.Id} for owning rule {owningRuleId} affected {affectedRows} rows instead of exactly one.");
        }
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
