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
    }
}
