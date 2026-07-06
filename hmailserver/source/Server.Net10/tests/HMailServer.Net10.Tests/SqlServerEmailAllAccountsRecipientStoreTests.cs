using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerEmailAllAccountsRecipientStoreTests
{
    [TestMethod]
    public void AccountSql_ReadsOnlyLegacyMassMailCandidateFieldsInAddressOrder()
    {
        var sql = SqlServerEmailAllAccountsRecipientStore.GetAccountsSql;

        StringAssert.Contains(sql, "accountid");
        StringAssert.Contains(sql, "accountaddress");
        StringAssert.Contains(sql, "accountactive");
        StringAssert.Contains(sql, "FROM hm_accounts");
        StringAssert.Contains(sql, "ORDER BY accountaddress ASC");
        AssertReadOnlyAndNonSecret(sql);
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("accountpassword", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("accountadusername", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DomainSql_ReadsOnlyNameAndActiveStateInNameOrder()
    {
        var sql = SqlServerEmailAllAccountsRecipientStore.GetDomainsSql;

        StringAssert.Contains(sql, "domainname");
        StringAssert.Contains(sql, "domainactive");
        StringAssert.Contains(sql, "FROM hm_domains");
        StringAssert.Contains(sql, "ORDER BY domainname ASC");
        AssertReadOnlyAndNonSecret(sql);
        Assert.IsFalse(sql.Contains("domainpostmaster", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("domaindkim", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertReadOnlyAndNonSecret(string sql)
    {
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("password", StringComparison.OrdinalIgnoreCase));
    }
}
