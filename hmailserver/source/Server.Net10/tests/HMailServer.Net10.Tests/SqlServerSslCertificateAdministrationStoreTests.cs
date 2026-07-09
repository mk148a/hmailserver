using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSslCertificateAdministrationStoreTests
{
    [TestMethod]
    public void GetSslCertificatesSql_UsesLegacyCertificateTableColumnsAndNameOrdering()
    {
        var sql = SqlServerSslCertificateAdministrationStore.GetSslCertificatesSql;

        StringAssert.Contains(sql, "FROM hm_sslcertificates");
        StringAssert.Contains(sql, "sslcertificateid");
        StringAssert.Contains(sql, "sslcertificatename");
        StringAssert.Contains(sql, "sslcertificatefile");
        StringAssert.Contains(sql, "sslprivatekeyfile");
        StringAssert.Contains(sql, "ORDER BY sslcertificatename ASC");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ClearSslCertificatesSql_DeletesOnlyLegacyCertificateRows()
    {
        var sql = SqlServerSslCertificateAdministrationStore.ClearSslCertificatesSql;

        StringAssert.Contains(sql, "DELETE FROM hm_sslcertificates");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("sslcertificatefile", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("sslprivatekeyfile", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeleteSslCertificateByIdSql_DeletesOnlyMatchingLegacyCertificateRow()
    {
        var sql = SqlServerSslCertificateAdministrationStore.DeleteSslCertificateByIdSql;

        StringAssert.Contains(sql, "DELETE FROM hm_sslcertificates");
        StringAssert.Contains(sql, "WHERE sslcertificateid = @id");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("sslcertificatefile", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("sslprivatekeyfile", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UpdateSslCertificateSql_UpdatesOnlyMatchingLegacyCertificateRow()
    {
        var sql = SqlServerSslCertificateAdministrationStore.UpdateSslCertificateSql;

        StringAssert.Contains(sql, "UPDATE hm_sslcertificates");
        StringAssert.Contains(sql, "SET sslcertificatename = @name");
        StringAssert.Contains(sql, "sslcertificatefile = @certificateFile");
        StringAssert.Contains(sql, "sslprivatekeyfile = @privateKeyFile");
        StringAssert.Contains(sql, "WHERE sslcertificateid = @id");
        Assert.IsFalse(sql.Contains(" JOIN ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT ", StringComparison.OrdinalIgnoreCase));
    }
}
