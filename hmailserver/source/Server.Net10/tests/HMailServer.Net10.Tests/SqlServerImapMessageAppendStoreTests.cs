using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerImapMessageAppendStoreTests
{
    [TestMethod]
    public void AppendSql_AllocatesUidInsertsMessageAndQueuesIndexing()
    {
        StringAssert.Contains(SqlServerImapMessageAppendStore.AllocateUidSql, "SET foldercurrentuid = foldercurrentuid + 1");
        StringAssert.Contains(SqlServerImapMessageAppendStore.AllocateUidSql, "OUTPUT INSERTED.foldercurrentuid");
        StringAssert.Contains(SqlServerImapMessageAppendStore.InsertAppendedMessageSql, "OUTPUT INSERTED.messageid");
        StringAssert.Contains(SqlServerImapMessageAppendStore.InsertAppendedMessageSql, "messageuid");
        StringAssert.Contains(SqlServerImapMessageAppendStore.QueueAppendedMessageForIndexingSql, "hm_message_search_queue");
    }
}
