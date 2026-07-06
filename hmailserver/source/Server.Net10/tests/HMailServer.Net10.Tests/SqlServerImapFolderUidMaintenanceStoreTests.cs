using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapFolderUidMaintenanceStoreTests
{
    [TestMethod]
    public void ReadSql_SelectsOnlyFolderMaximumMessageUids()
    {
        var sql = SqlServerImapFolderUidMaintenanceStore.ReadFolderMaximumUidsSql;

        StringAssert.Contains(sql, "SELECT messagefolderid, MAX(messageuid) AS messageuid");
        StringAssert.Contains(sql, "FROM hm_messages");
        StringAssert.Contains(sql, "GROUP BY messagefolderid");
        Assert.IsFalse(sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("messagefilename", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_messagerecipients", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void AdvanceSql_OnlyRaisesSelectedFolderCurrentUid()
    {
        var sql = SqlServerImapFolderUidMaintenanceStore.AdvanceFolderUidSql;

        StringAssert.Contains(sql, "UPDATE hm_imapfolders");
        StringAssert.Contains(sql, "SET foldercurrentuid = @MessageUid");
        StringAssert.Contains(sql, "WHERE folderid = @MessageFolderId");
        StringAssert.Contains(sql, "foldercurrentuid < @MessageUid");
        Assert.IsFalse(sql.Contains("hm_messages", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("messageuid =", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("foldercurrentuid -", StringComparison.OrdinalIgnoreCase));
    }
}
