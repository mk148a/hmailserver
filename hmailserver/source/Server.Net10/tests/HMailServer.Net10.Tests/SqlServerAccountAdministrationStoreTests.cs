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
        StringAssert.Contains(sql, "FROM hm_accounts");
        StringAssert.Contains(sql, "WHERE accountdomainid = @DomainID");
        StringAssert.Contains(sql, "ORDER BY accountaddress ASC");
    }
}
