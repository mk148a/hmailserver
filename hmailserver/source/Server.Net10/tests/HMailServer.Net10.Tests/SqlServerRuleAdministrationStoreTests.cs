using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerRuleAdministrationStoreTests
{
    [TestMethod]
    public void GetRulesSql_UsesLegacyRuleTableAccountFilterAndSortOrdering()
    {
        var sql = SqlServerRuleAdministrationStore.GetRulesSql;

        StringAssert.Contains(sql, "ruleid");
        StringAssert.Contains(sql, "ruleaccountid");
        StringAssert.Contains(sql, "rulename");
        StringAssert.Contains(sql, "ruleactive");
        StringAssert.Contains(sql, "ruleuseand");
        StringAssert.Contains(sql, "rulesortorder");
        StringAssert.Contains(sql, "FROM hm_rules");
        StringAssert.Contains(sql, "WHERE ruleaccountid = @AccountID");
        StringAssert.Contains(sql, "ORDER BY rulesortorder ASC, ruleid ASC");
        Assert.IsFalse(sql.Contains("hm_rule_criterias", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_rule_actions", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void GetBackupRulesSql_PreservesLegacyBackupOrderingWithoutComTieBreaker()
    {
        var sql = SqlServerRuleAdministrationStore.GetBackupRulesSql;

        StringAssert.Contains(sql, "FROM hm_rules");
        StringAssert.Contains(sql, "WHERE ruleaccountid = @AccountID");
        StringAssert.Contains(sql, "ORDER BY rulesortorder ASC");
        Assert.IsFalse(sql.Contains("ORDER BY rulesortorder ASC, ruleid ASC", StringComparison.Ordinal));
        Assert.IsFalse(sql.Contains("hm_rule_criterias", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_rule_actions", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteRuleSql_IsOwnerScopedTransactionalAndCleansRuleDependents()
    {
        var sql = SqlServerRuleAdministrationStore.DeleteRuleSql;

        StringAssert.Contains(sql, "SET XACT_ABORT ON");
        StringAssert.Contains(sql, "BEGIN TRANSACTION");
        StringAssert.Contains(sql, "UPDLOCK, HOLDLOCK");
        StringAssert.Contains(sql, "WHERE ruleid = @RuleId");
        StringAssert.Contains(sql, "AND ruleaccountid = @AccountID");
        StringAssert.Contains(sql, "DELETE FROM hm_rule_actions");
        StringAssert.Contains(sql, "WHERE actionruleid = @RuleId");
        StringAssert.Contains(sql, "DELETE FROM hm_rule_criterias");
        StringAssert.Contains(sql, "WHERE criteriaruleid = @RuleId");
        StringAssert.Contains(sql, "COMMIT TRANSACTION");
        StringAssert.Contains(sql, "ROLLBACK TRANSACTION");
        StringAssert.Contains(sql, "SELECT @Deleted");
    }

    [TestMethod]
    public void DeleteRuleAsync_ExposesOwnerAndRuleScopedBooleanStoreContract()
    {
        var method = typeof(SqlServerRuleAdministrationStore).GetMethod(nameof(SqlServerRuleAdministrationStore.DeleteRuleAsync));

        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(ValueTask<bool>), method.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(int), typeof(int), typeof(CancellationToken) },
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }
}
