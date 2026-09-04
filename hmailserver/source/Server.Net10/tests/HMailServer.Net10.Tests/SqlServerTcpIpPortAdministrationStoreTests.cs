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
        StringAssert.Contains(sql, "sslcertificatename");
        StringAssert.Contains(sql, "LEFT JOIN hm_sslcertificates");
        StringAssert.Contains(sql, "ORDER BY portaddress1 ASC, portaddress2 ASC, portnumber ASC");
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

    [TestMethod]
    public void DeleteTcpIpPortByIdSql_UsesPortIdentityPredicate()
    {
        var sql = SqlServerTcpIpPortAdministrationStore.DeleteTcpIpPortByIdSql;

        StringAssert.Contains(sql, "DELETE FROM hm_tcpipports");
        StringAssert.Contains(sql, "WHERE portid = @id");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateTcpIpPortSql_UsesAllLegacyMutableColumnsAndIdentityPredicate()
    {
        var sql = SqlServerTcpIpPortAdministrationStore.UpdateTcpIpPortSql;

        StringAssert.Contains(sql, "UPDATE hm_tcpipports");
        StringAssert.Contains(sql, "portprotocol = @protocol");
        StringAssert.Contains(sql, "portnumber = @portNumber");
        StringAssert.Contains(sql, "portaddress1 = @address1");
        StringAssert.Contains(sql, "portaddress2 = @address2");
        StringAssert.Contains(sql, "portconnectionsecurity = @connectionSecurity");
        StringAssert.Contains(sql, "portsslcertificateid = @sslCertificateId");
        StringAssert.Contains(sql, "WHERE portid = @id");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }
    [TestMethod]
    public void DeleteAllTcpIpPortsSql_RemovesEveryPort()
    {
        var sql = SqlServerTcpIpPortAdministrationStore.DeleteAllTcpIpPortsSql;
        StringAssert.Contains(sql, "DELETE FROM hm_tcpipports");
        Assert.IsFalse(sql.Contains("WHERE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ResolveSslCertificateIdByNameSql_UsesLegacyCertificateNameLookup()
    {
        var sql = SqlServerTcpIpPortAdministrationStore.ResolveSslCertificateIdByNameSql;

        StringAssert.Contains(sql, "FROM hm_sslcertificates");
        StringAssert.Contains(sql, "sslcertificateid");
        StringAssert.Contains(sql, "sslcertificatename = @name");
        StringAssert.Contains(sql, "TOP (1)");
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }
}
