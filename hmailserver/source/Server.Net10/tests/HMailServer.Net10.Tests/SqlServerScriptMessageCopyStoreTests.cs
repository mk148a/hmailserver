using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerScriptMessageCopyStoreTests
{
    [TestMethod]
    public void CopySql_RequiresSameAccountAndCreatesSearchableDeliveredMessage()
    {
        StringAssert.Contains(
            SqlServerScriptMessageCopyStore.LoadDestinationFolderSql,
            "f.folderaccountid = @SourceAccountId");
        StringAssert.Contains(
            SqlServerScriptMessageCopyStore.AllocateDestinationUidSql,
            "UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)");
        StringAssert.Contains(
            SqlServerScriptMessageCopyStore.InsertCopiedMessageSql,
            "@DestinationFolderId");
        StringAssert.Contains(
            SqlServerScriptMessageCopyStore.InsertCopiedMessageSql,
            "messagetype");
        StringAssert.Contains(
            SqlServerScriptMessageCopyStore.QueueCopiedMessageForIndexingSql,
            "hm_message_search_queue");
    }
}
