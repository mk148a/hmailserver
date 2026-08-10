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


    public const string InsertRuleSql = """
        INSERT INTO hm_rules
            (ruleaccountid, rulename, ruleactive, ruleuseand, rulesortorder)
        OUTPUT INSERTED.ruleid
        VALUES
            (@AccountID, @Name, @Active, @UseAnd, @SortOrder);
        """;

    public const string UpdateRuleSql = """
        UPDATE hm_rules
        SET ruleaccountid = @AccountID,
            rulename = @Name,
            ruleactive = @Active,
            ruleuseand = @UseAnd,
            rulesortorder = @SortOrder
        WHERE ruleid = @RuleId AND ruleaccountid = @AccountID;
        """;

    public const string MoveRuleSql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @OrderedRules TABLE
(
    ruleid int NOT NULL PRIMARY KEY,
    position int NOT NULL
);

INSERT INTO @OrderedRules (ruleid, position)
SELECT
    ruleid,
    ROW_NUMBER() OVER (ORDER BY rulesortorder ASC, ruleid ASC)
FROM hm_rules WITH (UPDLOCK, HOLDLOCK)
WHERE ruleaccountid = @AccountID;

DECLARE @CurrentPosition int =
    (SELECT position FROM @OrderedRules WHERE ruleid = @RuleId);
DECLARE @RuleCount int = (SELECT COUNT(*) FROM @OrderedRules);
DECLARE @TargetPosition int =
    CASE WHEN @MoveUp = 1 THEN @CurrentPosition - 1 ELSE @CurrentPosition + 1 END;
DECLARE @Moved bit = 0;

IF @CurrentPosition IS NOT NULL
   AND @TargetPosition BETWEEN 1 AND @RuleCount
BEGIN
    UPDATE hm_rules
    SET rulesortorder = rulesortorder + @RuleCount + 1
    WHERE ruleaccountid = @AccountID;

    UPDATE rule
    SET rulesortorder =
        CASE
            WHEN ordered.position = @CurrentPosition THEN @TargetPosition
            WHEN ordered.position = @TargetPosition THEN @CurrentPosition
            ELSE ordered.position
        END
    FROM hm_rules AS rule
    INNER JOIN @OrderedRules AS ordered ON ordered.ruleid = rule.ruleid
    WHERE rule.ruleaccountid = @AccountID;

    SET @Moved = 1;
END;

COMMIT TRANSACTION;
SELECT @Moved;
""";
    private readonly SqlServerConnectionFactory? _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerRuleAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerRuleAdministrationStore(SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _transactionContext = transactionContext;
    }

    public async ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetRulesAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var lease = await SqlServerCommandLease.OpenAsync(_connectionFactory, _transactionContext, GetRulesSql, cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
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
        await using var lease = await SqlServerCommandLease.OpenAsync(_connectionFactory, _transactionContext, GetBackupRulesSql, cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
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
        await using var lease = await SqlServerCommandLease.OpenAsync(_connectionFactory, _transactionContext, DeleteRuleSql, cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = ruleId;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;

        var deleted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return deleted is not null
            && Convert.ToInt32(deleted, CultureInfo.InvariantCulture) != 0;
    }

    public async ValueTask<int> InsertRuleAsync(
        int accountId,
        RuleAdministrationSnapshot rule,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        await using var lease = await SqlServerCommandLease.OpenAsync(_connectionFactory, _transactionContext, InsertRuleSql, cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = rule.Name;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = rule.Active ? 1 : 0;
        command.Parameters.Add("@UseAnd", SqlDbType.TinyInt).Value = rule.UseAnd ? 1 : 0;
        command.Parameters.Add("@SortOrder", SqlDbType.Int).Value = rule.SortOrder;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }
    public async ValueTask<bool> UpdateRuleAsync(
        int accountId,
        RuleAdministrationSnapshot rule,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        await using var lease = await SqlServerCommandLease.OpenAsync(_connectionFactory, _transactionContext, UpdateRuleSql, cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = rule.Id;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = rule.Name;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = rule.Active ? 1 : 0;
        command.Parameters.Add("@UseAnd", SqlDbType.TinyInt).Value = rule.UseAnd ? 1 : 0;
        command.Parameters.Add("@SortOrder", SqlDbType.Int).Value = rule.SortOrder;
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }

    public async ValueTask<bool> MoveRuleAsync(
        int accountId,
        int ruleId,
        bool moveUp,
        CancellationToken cancellationToken)
    {
        await using var lease = await SqlServerCommandLease.OpenAsync(_connectionFactory, _transactionContext, MoveRuleSql, cancellationToken).ConfigureAwait(false);
        var command = lease.Command;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = ruleId;
        command.Parameters.Add("@MoveUp", SqlDbType.Bit).Value = moveUp;

        var moved = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return moved is not null
            && Convert.ToInt32(moved, CultureInfo.InvariantCulture) != 0;
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
