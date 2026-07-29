using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerRuleCriteriaAdministrationStoreTests
{
    [TestMethod]
    public void GetRuleCriteriaSql_UsesLegacyColumnsRuleFilterAndOrderingWithoutMutationOrExecutionJoins()
    {
        var sql = SqlServerRuleCriteriaAdministrationStore.GetRuleCriteriaSql;

        StringAssert.Contains(sql, "criteriaid");
        StringAssert.Contains(sql, "criteriaruleid");
        StringAssert.Contains(sql, "criteriamatchvalue");
        StringAssert.Contains(sql, "criteriausepredefined");
        StringAssert.Contains(sql, "criteriapredefinedfield");
        StringAssert.Contains(sql, "criteriamatchtype");
        StringAssert.Contains(sql, "criteriaheadername");
        StringAssert.Contains(sql, "FROM hm_rule_criterias");
        StringAssert.Contains(sql, "WHERE criteriaruleid = @RuleId");
        StringAssert.Contains(sql, "ORDER BY criteriaid ASC");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_rule_actions", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteRuleCriteriaByIdSql_UsesRuleAndCriteriaPredicatesAndParameters()
    {
        var sql = SqlServerRuleCriteriaAdministrationStore.DeleteRuleCriteriaByIdSql;

        StringAssert.Contains(sql, "DELETE FROM hm_rule_criterias");
        StringAssert.Contains(sql, "WHERE criteriaruleid = @RuleId");
        StringAssert.Contains(sql, "AND criteriaid = @CriteriaId");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_rules", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void SaveRuleCriteriaSql_UsesRuleAndCriteriaPredicatesAndAllPersistedFields()
    {
        var sql = SqlServerRuleCriteriaAdministrationStore.SaveRuleCriteriaSql;

        StringAssert.Contains(sql, "UPDATE hm_rule_criterias");
        StringAssert.Contains(sql, "SET criteriaruleid = @RuleId");
        StringAssert.Contains(sql, "WHERE criteriaruleid = @OwningRuleId");
        StringAssert.Contains(sql, "criteriausepredefined = @UsePredefined");
        StringAssert.Contains(sql, "criteriapredefinedfield = @PredefinedField");
        StringAssert.Contains(sql, "criteriaheadername = @HeaderField");
        StringAssert.Contains(sql, "criteriamatchtype = @MatchType");
        StringAssert.Contains(sql, "criteriamatchvalue = @MatchValue");
        StringAssert.Contains(sql, "AND criteriaid = @CriteriaId");
        Assert.IsFalse(sql.Contains("WHERE criteriaruleid = @RuleId", StringComparison.Ordinal));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }
}
