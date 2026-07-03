using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerAccountAdministrationStoreTests
{
    [TestMethod]
    public void GetAccountsSql_UsesLegacyAccountTableDomainFilterAndAddressOrdering()
    {
        var sql = SqlServerAccountAdministrationStore.GetAccountsSql;

        AssertLegacyAccountProjection(sql);
        StringAssert.Contains(sql, "WHERE accountdomainid = @DomainID");
        StringAssert.Contains(sql, "ORDER BY accountaddress ASC");
    }

    [TestMethod]
    public void GetAccountByIdSql_UsesLegacyAccountTableIdFilterWithoutSecretColumns()
    {
        var sql = SqlServerAccountAdministrationStore.GetAccountByIdSql;

        AssertLegacyAccountProjection(sql);
        StringAssert.Contains(sql, "WHERE accountid = @AccountID");
        Assert.IsFalse(sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertLegacyAccountProjection(string sql)
    {
        StringAssert.Contains(sql, "accountid");
        StringAssert.Contains(sql, "accountdomainid");
        StringAssert.Contains(sql, "accountaddress");
        StringAssert.Contains(sql, "accountactive");
        StringAssert.Contains(sql, "accountadminlevel");
        StringAssert.Contains(sql, "accountisad");
        StringAssert.Contains(sql, "accountaddomain");
        StringAssert.Contains(sql, "accountadusername");
        StringAssert.Contains(sql, "accountmaxsize");
        StringAssert.Contains(sql, "accountsizebytes");
        StringAssert.Contains(sql, "accountlastlogontime");
        StringAssert.Contains(sql, "accountpersonfirstname");
        StringAssert.Contains(sql, "accountpersonlastname");
        StringAssert.Contains(sql, "accountvacationmessageon");
        StringAssert.Contains(sql, "accountvacationmessage");
        StringAssert.Contains(sql, "accountvacationsubject");
        StringAssert.Contains(sql, "accountvacationexpires");
        StringAssert.Contains(sql, "accountvacationexpiredate");
        StringAssert.Contains(sql, "accountvacationabortspamflagged");
        StringAssert.Contains(sql, "accountforwardenabled");
        StringAssert.Contains(sql, "accountforwardaddress");
        StringAssert.Contains(sql, "accountforwardkeeporiginal");
        StringAssert.Contains(sql, "accountforwardabortspamflagged");
        StringAssert.Contains(sql, "accountenablesignature");
        StringAssert.Contains(sql, "accountsignatureplaintext");
        StringAssert.Contains(sql, "accountsignaturehtml");
        StringAssert.Contains(sql, "FROM hm_accounts");
        StringAssert.Contains(sql, "FROM hm_messages");
        StringAssert.Contains(sql, "SUM(CAST(messagesize AS bigint))");
        StringAssert.Contains(sql, "messageaccountid = hm_accounts.accountid");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("accountpassword", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("messagefilename", StringComparison.OrdinalIgnoreCase));
    }
}
