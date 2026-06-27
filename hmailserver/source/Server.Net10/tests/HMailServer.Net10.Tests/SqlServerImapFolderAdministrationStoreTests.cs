using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapFolderAdministrationStoreTests
{
    [TestMethod]
    public void GetRootFoldersSql_UsesLegacyFolderTableAccountAndRootFiltersAndIdOrdering()
    {
        var sql = SqlServerImapFolderAdministrationStore.GetRootFoldersSql;

        StringAssert.Contains(sql, "folderid");
        StringAssert.Contains(sql, "folderaccountid");
        StringAssert.Contains(sql, "folderparentid");
        StringAssert.Contains(sql, "foldername");
        StringAssert.Contains(sql, "folderissubscribed");
        StringAssert.Contains(sql, "foldercurrentuid");
        StringAssert.Contains(sql, "foldercreationtime");
        StringAssert.Contains(sql, "FROM hm_imapfolders");
        StringAssert.Contains(sql, "folderaccountid = @AccountID");
        StringAssert.Contains(sql, "folderparentid = -1");
        StringAssert.Contains(sql, "ORDER BY folderid ASC");
        Assert.IsFalse(sql.Contains("hm_messages", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sql.Contains("hm_acl", StringComparison.OrdinalIgnoreCase));
    }
}
