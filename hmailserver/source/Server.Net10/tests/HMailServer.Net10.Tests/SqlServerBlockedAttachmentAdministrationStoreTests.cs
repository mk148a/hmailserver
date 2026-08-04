using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerBlockedAttachmentAdministrationStoreTests
{
    [TestMethod]
    public void GetBlockedAttachmentsSql_UsesLegacyBlockedAttachmentTableColumnsAndWildcardOrdering()
    {
        var sql = SqlServerBlockedAttachmentAdministrationStore.GetBlockedAttachmentsSql;

        StringAssert.Contains(sql, "FROM hm_blocked_attachments");
        StringAssert.Contains(sql, "baid");
        StringAssert.Contains(sql, "bawildcard");
        StringAssert.Contains(sql, "badescription");
        StringAssert.Contains(sql, "ORDER BY bawildcard ASC");
    }

    [TestMethod]
    public void GetBlockedAttachmentsSql_RemainsReadOnlyAndDoesNotTouchScannerRuntime()
    {
        var sql = SqlServerBlockedAttachmentAdministrationStore.GetBlockedAttachmentsSql;

        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("VirusScannerTester", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("process", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("xp_", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InsertBlockedAttachmentSql_UsesGeneratedIdentityAndLegacyColumns()
    {
        var sql = SqlServerBlockedAttachmentAdministrationStore.InsertBlockedAttachmentSql;

        StringAssert.Contains(sql, "INSERT INTO hm_blocked_attachments");
        StringAssert.Contains(sql, "bawildcard");
        StringAssert.Contains(sql, "badescription");
        StringAssert.Contains(sql, "OUTPUT INSERTED.baid");
        StringAssert.Contains(sql, "@wildcard");
        StringAssert.Contains(sql, "@description");
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }
}
