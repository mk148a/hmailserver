using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerRuleAdministrationStore : IRuleAdministrationStore, IBackupRuleAdministrationStore
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

    public const string GetBackupRulesSql = """
SELECT
    ruleid,
    ruleaccountid,
    rulename,
    ruleactive,
    ruleuseand,
    rulesortorder
FROM hm_rules
WHERE ruleaccountid = @AccountID
ORDER BY rulesortorder ASC;
""";

    public const string DeleteRuleSql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Rule TABLE (ruleid int NOT NULL PRIMARY KEY);
INSERT INTO @Rule (ruleid)
SELECT ruleid
FROM hm_rules WITH (UPDLOCK, HOLDLOCK)
WHERE ruleid = @RuleId
  AND ruleaccountid = @AccountID;

DECLARE @Deleted bit = 0;
IF EXISTS (SELECT 1 FROM @Rule)
BEGIN
    DELETE FROM hm_rule_actions
    WHERE actionruleid = @RuleId;

    DELETE FROM hm_rule_criterias
    WHERE criteriaruleid = @RuleId;

    DELETE FROM hm_rules
    WHERE ruleid = @RuleId
      AND ruleaccountid = @AccountID;

    IF @@ROWCOUNT = 1
        SET @Deleted = 1;
END;

IF @Deleted = 1
    COMMIT TRANSACTION;
ELSE
    ROLLBACK TRANSACTION;

SELECT @Deleted;
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

    public async ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetBackupRulesAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetBackupRulesSql, connection);
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

    public async ValueTask<bool> DeleteRuleAsync(
        int accountId,
        int ruleId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteRuleSql, connection);
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = ruleId;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;

        var deleted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return deleted is not null
            && Convert.ToInt32(deleted, CultureInfo.InvariantCulture) != 0;
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
