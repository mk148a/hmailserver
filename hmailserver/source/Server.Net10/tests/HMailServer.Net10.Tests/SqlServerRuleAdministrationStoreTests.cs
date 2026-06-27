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
}
