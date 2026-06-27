using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerIncomingRelayAdministrationStoreTests
{
    [TestMethod]
    public void GetIncomingRelaysSql_UsesLegacyIncomingRelayTableColumnsAndNameOrdering()
    {
        var sql = SqlServerIncomingRelayAdministrationStore.GetIncomingRelaysSql;

        StringAssert.Contains(sql, "FROM hm_incoming_relays");
        StringAssert.Contains(sql, "relayid");
        StringAssert.Contains(sql, "relayname");
        StringAssert.Contains(sql, "relaylowerip1");
        StringAssert.Contains(sql, "relaylowerip2");
        StringAssert.Contains(sql, "relayupperip1");
        StringAssert.Contains(sql, "relayupperip2");
        StringAssert.Contains(sql, "ORDER BY relayname ASC");
    }
}
