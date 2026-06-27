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
    }
}
