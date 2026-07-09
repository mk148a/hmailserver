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
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteIncomingRelayByIdSql_DeletesOnlyMatchingLegacyIncomingRelayRow()
    {
        var sql = SqlServerIncomingRelayAdministrationStore.DeleteIncomingRelayByIdSql;

        StringAssert.Contains(sql, "DELETE FROM hm_incoming_relays");
        StringAssert.Contains(sql, "WHERE relayid = @id");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT ", StringComparison.OrdinalIgnoreCase));
    }
}
