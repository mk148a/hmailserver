using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerAccountAdministrationStoreTests
{
    [TestMethod]
    public void GetAccountsSql_UsesLegacyAccountTableDomainFilterAndAddressOrdering()
    {
        var sql = SqlServerAccountAdministrationStore.GetAccountsSql;

        StringAssert.Contains(sql, "accountid");
        StringAssert.Contains(sql, "accountdomainid");
        StringAssert.Contains(sql, "accountaddress");
        StringAssert.Contains(sql, "accountactive");
        StringAssert.Contains(sql, "accountadminlevel");
        StringAssert.Contains(sql, "accountmaxsize");
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
        StringAssert.Contains(sql, "WHERE accountdomainid = @DomainID");
        StringAssert.Contains(sql, "ORDER BY accountaddress ASC");
    }
}
