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
}
