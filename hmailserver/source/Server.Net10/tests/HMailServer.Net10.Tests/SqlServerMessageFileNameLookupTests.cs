using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerMessageFileNameLookupTests
{
    [TestMethod]
    public void LookupSql_ReadsOnlyStoredFilenameForSelectedMessageId()
    {
        var sql = SqlServerMessageFileNameLookup.GetFileNameByMessageIdSql;

        StringAssert.Contains(sql, "SELECT messagefilename");
        StringAssert.Contains(sql, "FROM hm_messages");
        StringAssert.Contains(sql, "WHERE messageid = @MessageID");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_messagerecipients", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("filecontent", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ReverseLookupSql_ReadsOnlyMessageIdForExactStoredFilename()
    {
        var sql = SqlServerMessageFileNameLookup.GetMessageIdByFileNameSql;

        StringAssert.Contains(sql, "SELECT messageid");
        StringAssert.Contains(sql, "FROM hm_messages");
        StringAssert.Contains(sql, "WHERE messagefilename = @FileName");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_messagerecipients", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("filecontent", StringComparison.OrdinalIgnoreCase));
    }
}
