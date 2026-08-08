using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDomainAdministrationStoreTests
{
    [TestMethod]
    public void GetDomainsSql_UsesLegacyDomainTableAndNameOrdering()
    {
        var sql = SqlServerDomainAdministrationStore.GetDomainsSql;

        StringAssert.Contains(sql, "domainid");
        StringAssert.Contains(sql, "domainname");
        StringAssert.Contains(sql, "domainactive");
        StringAssert.Contains(sql, "domainpostmaster");
        StringAssert.Contains(sql, "domainmaxmessagesize");
        StringAssert.Contains(sql, "domainuseplusaddressing");
        StringAssert.Contains(sql, "domainplusaddressingchar");
        StringAssert.Contains(sql, "domainaddomain");
        StringAssert.Contains(sql, "domainallocatedsize");
        StringAssert.Contains(sql, "domainsizebytes");
        StringAssert.Contains(sql, "domainmaxsize");
        StringAssert.Contains(sql, "domainmaxnoofaccounts");
        StringAssert.Contains(sql, "domainmaxnoofaliases");
        StringAssert.Contains(sql, "domainmaxnoofdistributionlists");
        StringAssert.Contains(sql, "domainlimitationsenabled");
        StringAssert.Contains(sql, "domainmaxaccountsize");
        StringAssert.Contains(sql, "domainenablesignature");
        StringAssert.Contains(sql, "domainsignaturemethod");
        StringAssert.Contains(sql, "domainsignatureplaintext");
        StringAssert.Contains(sql, "domainsignaturehtml");
        StringAssert.Contains(sql, "domainaddsignaturestoreplies");
        StringAssert.Contains(sql, "domainaddsignaturestolocalemail");
        StringAssert.Contains(sql, "domainantispamoptions");
        StringAssert.Contains(sql, "domaindkimselector");
        StringAssert.Contains(sql, "domaindkimprivatekeyfile");
        StringAssert.Contains(sql, "FROM hm_domains");
        StringAssert.Contains(sql, "ORDER BY domainname ASC");
    }

    [TestMethod]
    public void GetDomainsSql_KeepsDkimAndSignatureProjectionReadOnlyAndDomainScoped()
    {
        var sql = SqlServerDomainAdministrationStore.GetDomainsSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("OPENROWSET", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("BULK", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(sql, "FROM hm_domains");
        StringAssert.Contains(sql, "FROM hm_accounts");
        StringAssert.Contains(sql, "SUM(CAST(accountmaxsize AS bigint))");
        StringAssert.Contains(sql, "accountdomainid = hm_domains.domainid");
        StringAssert.Contains(sql, "FROM hm_messages");
        StringAssert.Contains(sql, "SUM(CAST(messagesize AS bigint))");
        StringAssert.Contains(sql, "messageaccountid IN");
        StringAssert.Contains(sql, "SELECT accountdomainid");
        StringAssert.Contains(sql, "domainaddomain");
        StringAssert.Contains(sql, "domainantispamoptions");
        StringAssert.Contains(sql, "domaindkimselector");
        StringAssert.Contains(sql, "domaindkimprivatekeyfile");
        StringAssert.Contains(sql, "domainenablesignature");
        StringAssert.Contains(sql, "domainsignaturemethod");
        StringAssert.Contains(sql, "domainsignatureplaintext");
        StringAssert.Contains(sql, "domainsignaturehtml");
        StringAssert.Contains(sql, "domainaddsignaturestoreplies");
        StringAssert.Contains(sql, "domainaddsignaturestolocalemail");
    }
    [TestMethod]
    public void InsertDomainSql_UsesLegacyDomainTableColumnsAndIdentityOutput()
    {
        var sql = SqlServerDomainAdministrationStore.InsertDomainSql;
        StringAssert.Contains(sql, "INSERT INTO hm_domains");
        foreach (var column in new[]
        {
            "domainname",
            "domainactive",
            "domainpostmaster",
            "domainmaxsize",
            "domainaddomain",
            "domainmaxmessagesize",
            "domainmaxaccountsize",
            "domainuseplusaddressing",
            "domainplusaddressingchar",
            "domainantispamoptions",
            "domainenablesignature",
            "domainsignaturemethod",
            "domainsignatureplaintext",
            "domainsignaturehtml",
            "domainaddsignaturestoreplies",
            "domainaddsignaturestolocalemail",
            "domainmaxnoofaccounts",
            "domainmaxnoofaliases",
            "domainmaxnoofdistributionlists",
            "domainlimitationsenabled",
            "domaindkimselector",
            "domaindkimprivatekeyfile"
        })
        {
            StringAssert.Contains(sql, column);
        }

        StringAssert.Contains(sql, "OUTPUT INSERTED.domainid");
        StringAssert.Contains(sql, "@Name");
        StringAssert.Contains(sql, "@AntiSpamOptions");
        StringAssert.Contains(sql, "@LimitationsEnabled");
        StringAssert.Contains(sql, "@DkimPrivateKeyFile");
        Assert.IsFalse(sql.Contains("WHERE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }
    [TestMethod]
    public void UpdateDomainSql_UsesLegacyDomainTableColumnsAndIdentityPredicate()
    {
        var sql = SqlServerDomainAdministrationStore.UpdateDomainSql;
        StringAssert.Contains(sql, "UPDATE hm_domains");
        StringAssert.Contains(sql, "domainname = @Name");
        StringAssert.Contains(sql, "domainactive = @Active");
        StringAssert.Contains(sql, "domainantispamoptions = @AntiSpamOptions");
        StringAssert.Contains(sql, "domainlimitationsenabled = @LimitationsEnabled");
        StringAssert.Contains(sql, "domaindkimprivatekeyfile = @DkimPrivateKeyFile");
        StringAssert.Contains(sql, "WHERE domainid = @ID");
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }
    [TestMethod]
    public void DeleteDomainByIdSql_IsTransactionalAndCascadesLegacyDomainDependents()
    {
        var sql = SqlServerDomainAdministrationStore.DeleteDomainByIdSql;
        StringAssert.Contains(sql, "DELETE FROM hm_domain_aliases WHERE dadomainid = @ID");
        StringAssert.Contains(sql, "DELETE FROM hm_distributionlistsrecipients");
        StringAssert.Contains(sql, "DELETE FROM hm_distributionlists WHERE distributionlistdomainid = @ID");
        StringAssert.Contains(sql, "DELETE FROM hm_aliases WHERE aliasdomainid = @ID");
        StringAssert.Contains(sql, "DELETE FROM hm_rules");
        StringAssert.Contains(sql, "DELETE FROM hm_accounts WHERE accountdomainid = @ID");
        StringAssert.Contains(sql, "DELETE FROM hm_domains WHERE domainid = @ID");
        StringAssert.Contains(sql, "BEGIN TRANSACTION");
        StringAssert.Contains(sql, "COMMIT TRANSACTION");
        StringAssert.Contains(sql, "ROLLBACK TRANSACTION");
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteAllDomainsForRestoreSql_IsSetBasedAndKeepsLegacyOwnerOrder()
    {
        var sql = SqlServerDomainAdministrationStore.DeleteAllDomainsForRestoreSql;

        foreach (var table in new[]
        {
            "hm_domain_aliases",
            "hm_distributionlistsrecipients",
            "hm_distributionlists",
            "hm_aliases",
            "hm_rule_actions",
            "hm_rule_criterias",
            "hm_rules",
            "hm_messagerecipients",
            "hm_message_metadata",
            "hm_message_search_queue",
            "hm_message_search_documents",
            "hm_messages",
            "hm_acl",
            "hm_group_members",
            "hm_imapfolders",
            "hm_fetchaccounts_uids",
            "hm_fetchaccounts",
            "hm_accounts",
            "hm_domains"
        })
        {
            StringAssert.Contains(sql, $"DELETE FROM {table}");
        }

        StringAssert.Contains(sql, "WITH (UPDLOCK, HOLDLOCK)");
        StringAssert.Contains(sql, "@DomainIds");
        StringAssert.Contains(sql, "@AccountIds");
        StringAssert.Contains(sql, "@DistributionListIds");
        StringAssert.Contains(sql, "@RuleIds");
        StringAssert.Contains(sql, "@MessageIds");
        StringAssert.Contains(sql, "@FetchAccountIds");
        StringAssert.Contains(sql, "@FolderIds");
        Assert.IsTrue(sql.IndexOf("DELETE FROM hm_acl", StringComparison.OrdinalIgnoreCase)
            < sql.IndexOf("DELETE FROM hm_accounts", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(sql.IndexOf("DELETE FROM hm_imapfolders", StringComparison.OrdinalIgnoreCase)
            < sql.IndexOf("DELETE FROM hm_accounts", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("BEGIN TRANSACTION", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("COMMIT TRANSACTION", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("ROLLBACK TRANSACTION", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
    }
}
