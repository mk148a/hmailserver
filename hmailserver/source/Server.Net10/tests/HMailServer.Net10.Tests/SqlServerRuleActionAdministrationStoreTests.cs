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

    [TestMethod]
    public void SaveRuleActionSql_UsesRuleAndActionPredicatesAndAllPersistedFields()
    {
        var sql = SqlServerRuleActionAdministrationStore.SaveRuleActionSql;

        StringAssert.Contains(sql, "UPDATE hm_rule_actions");
        foreach (var column in new[]
                 {
                     "actionruleid = @RuleId", "actiontype = @Type", "actionimapfolder = @ImapFolder",
                     "actionsubject = @Subject", "actionfromname = @FromName",
                     "actionfromaddress = @FromAddress", "actionto = @To", "actionbody = @Body",
                     "actionfilename = @Filename", "actionsortorder = @SortOrder",
                     "actionscriptfunction = @ScriptFunction", "actionheader = @HeaderName",
                     "actionvalue = @Value", "actionrouteid = @RouteId",
                     "actionabortspamflagged = @AbortSpamFlagged"
                 })
        {
            StringAssert.Contains(sql, column);
        }

        StringAssert.Contains(sql, "SET actionruleid = @RuleId");
        StringAssert.Contains(sql, "WHERE actionruleid = @OwningRuleId");
        StringAssert.Contains(sql, "AND actionid = @ActionId");
        Assert.IsFalse(sql.Contains("WHERE actionruleid = @RuleId", StringComparison.Ordinal));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void SaveRuleActionOrderSql_UsesOwnerActionAndSortOrderPredicates()
    {
        var sql = SqlServerRuleActionAdministrationStore.SaveRuleActionOrderSql;

        StringAssert.Contains(sql, "UPDATE hm_rule_actions");
        StringAssert.Contains(sql, "actionsortorder = @SortOrder");
        StringAssert.Contains(sql, "WHERE actionruleid = @OwningRuleId");
        StringAssert.Contains(sql, "AND actionid = @ActionId");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertRuleActionSql_UsesGeneratedIdentityAndAllPersistedFields()
    {
        var sql = SqlServerRuleActionAdministrationStore.InsertRuleActionSql;

        StringAssert.Contains(sql, "INSERT INTO hm_rule_actions");
        StringAssert.Contains(sql, "OUTPUT INSERTED.actionid");
        StringAssert.Contains(sql, "(@RuleId, @Type, @ImapFolder, @Subject, @FromName");
        foreach (var column in new[]
                 {
                     "actionruleid", "actiontype", "actionimapfolder", "actionsubject", "actionfromname",
                     "actionfromaddress", "actionto", "actionbody", "actionfilename", "actionsortorder",
                     "actionscriptfunction", "actionheader", "actionvalue", "actionrouteid",
                     "actionabortspamflagged"
                 })
        {
            StringAssert.Contains(sql, column);
        }

        Assert.IsFalse(sql.Contains("MAX(", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("IDENT_CURRENT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("IDENTITY_INSERT", StringComparison.OrdinalIgnoreCase));
    }
}
