using HMailServer.Storage.SqlServer;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapMailboxSubscriptionStoreTests
{
    [TestMethod]
    public void UpdateMailboxSubscriptionSqlScopesAccountAndFolder()
    {
        var sql = SqlServerImapMailboxStore.UpdateMailboxSubscriptionSql;

        StringAssert.Contains(sql, "SET folderissubscribed = @Subscribed");
        StringAssert.Contains(sql, "folderid = @FolderId");
        StringAssert.Contains(sql, "folderaccountid = @FolderAccountId");
        Assert.IsFalse(sql.Contains("hm_messages", StringComparison.Ordinal));
    }
}
