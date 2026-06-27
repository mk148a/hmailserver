using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerFetchAccountAdministrationStoreTests
{
    [TestMethod]
    public void GetFetchAccountsSql_UsesLegacyFetchAccountTableAccountFilterAndIdOrdering()
    {
        var sql = SqlServerFetchAccountAdministrationStore.GetFetchAccountsSql;

        StringAssert.Contains(sql, "faid");
        StringAssert.Contains(sql, "faaccountid");
        StringAssert.Contains(sql, "faaccountname");
        StringAssert.Contains(sql, "faserveraddress");
        StringAssert.Contains(sql, "faserverport");
        StringAssert.Contains(sql, "faservertype");
        StringAssert.Contains(sql, "fausername");
        StringAssert.Contains(sql, "faminutes");
        StringAssert.Contains(sql, "fadaystokeep");
        StringAssert.Contains(sql, "faactive");
        StringAssert.Contains(sql, "faprocessmimerecipients");
        StringAssert.Contains(sql, "faprocessmimedate");
        StringAssert.Contains(sql, "faconnectionsecurity");
        StringAssert.Contains(sql, "fauseantispam");
        StringAssert.Contains(sql, "fauseantivirus");
        StringAssert.Contains(sql, "faenablerouterecipients");
        StringAssert.Contains(sql, "famimerecipientheaders");
        StringAssert.Contains(sql, "fanexttry");
        StringAssert.Contains(sql, "falocked");
        StringAssert.Contains(sql, "FROM hm_fetchaccounts");
        StringAssert.Contains(sql, "WHERE faaccountid = @AccountID");
        StringAssert.Contains(sql, "ORDER BY faid ASC");
        Assert.IsFalse(sql.Contains("fapassword", StringComparison.OrdinalIgnoreCase));
    }
}
