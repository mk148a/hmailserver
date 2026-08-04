using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerTcpIpPortAdministrationStoreTests
{
    [TestMethod]
    public void GetTcpIpPortsSql_UsesLegacyTcpIpPortTableColumnsAndAddressOrdering()
    {
        var sql = SqlServerTcpIpPortAdministrationStore.GetTcpIpPortsSql;

        StringAssert.Contains(sql, "FROM hm_tcpipports");
        StringAssert.Contains(sql, "portid");
        StringAssert.Contains(sql, "portprotocol");
        StringAssert.Contains(sql, "portnumber");
        StringAssert.Contains(sql, "portaddress1");
        StringAssert.Contains(sql, "portaddress2");
        StringAssert.Contains(sql, "portconnectionsecurity");
        StringAssert.Contains(sql, "portsslcertificateid");
        StringAssert.Contains(sql, "ORDER BY portaddress1 ASC, portaddress2 ASC, portnumber ASC");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertTcpIpPortSql_UsesLegacyColumnsAndGeneratedIdentity()
    {
        var sql = SqlServerTcpIpPortAdministrationStore.InsertTcpIpPortSql;

        StringAssert.Contains(sql, "INSERT INTO hm_tcpipports");
        StringAssert.Contains(sql, "portprotocol");
        StringAssert.Contains(sql, "portnumber");
        StringAssert.Contains(sql, "portaddress1");
        StringAssert.Contains(sql, "portaddress2");
        StringAssert.Contains(sql, "portconnectionsecurity");
        StringAssert.Contains(sql, "portsslcertificateid");
        StringAssert.Contains(sql, "OUTPUT INSERTED.portid");
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }
}
