using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerRuleActionAdministrationStoreTests
{
    [TestMethod]
    public void GetRuleActionsSql_UsesLegacyColumnsRuleFilterAndOrderingWithoutMutationOrExecutionJoins()
    {
        var sql = SqlServerRuleActionAdministrationStore.GetRuleActionsSql;

        foreach (var column in new[]
                 {
                     "actionid", "actionruleid", "actiontype", "actionsubject", "actionbody",
                     "actionfromname", "actionfromaddress", "actionfilename", "actionto",
                     "actionimapfolder", "actionscriptfunction", "actionheader", "actionvalue",
                     "actionrouteid", "actionabortspamflagged", "actionsortorder"
                 })
        {
            StringAssert.Contains(sql, column);
        }

        StringAssert.Contains(sql, "FROM hm_rule_actions");
        StringAssert.Contains(sql, "WHERE actionruleid = @RuleId");
        StringAssert.Contains(sql, "ORDER BY actionsortorder ASC");
        Assert.IsFalse(sql.Contains("actionid ASC", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_rules", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteRuleActionByIdSql_UsesRuleAndActionPredicatesAndParameters()
    {
        var sql = SqlServerRuleActionAdministrationStore.DeleteRuleActionByIdSql;

        StringAssert.Contains(sql, "DELETE FROM hm_rule_actions");
        StringAssert.Contains(sql, "WHERE actionruleid = @RuleId");
        StringAssert.Contains(sql, "AND actionid = @ActionId");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_rules", StringComparison.OrdinalIgnoreCase));
    }
}
