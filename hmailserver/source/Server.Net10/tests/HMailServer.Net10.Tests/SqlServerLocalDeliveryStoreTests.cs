using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerLocalDeliveryStoreTests
{
    [TestMethod]
    public void LocalDeliverySql_AllocatesInboxUidInsertsDeliveredMessageAndQueuesIndexing()
    {
        StringAssert.Contains(SqlServerLocalDeliveryStore.LoadAccountAddressSql, "FROM hm_accounts");
        StringAssert.Contains(SqlServerLocalDeliveryStore.LoadAccountAddressSql, "accountactive <> 0");
        StringAssert.Contains(SqlServerLocalDeliveryStore.AllocateInboxUidSql, "UPDATE hm_imapfolders WITH (UPDLOCK, ROWLOCK)");
        StringAssert.Contains(SqlServerLocalDeliveryStore.AllocateInboxUidSql, "folderparentid = -1");
        StringAssert.Contains(SqlServerLocalDeliveryStore.AllocateInboxUidSql, "LOWER(foldername) = 'inbox'");
        StringAssert.Contains(SqlServerLocalDeliveryStore.InsertDeliveredMessageSql, "messagetype");
        StringAssert.Contains(SqlServerLocalDeliveryStore.InsertDeliveredMessageSql, "2,");
        StringAssert.Contains(SqlServerLocalDeliveryStore.InsertDeliveredMessageSql, "messageuid");
        StringAssert.Contains(SqlServerLocalDeliveryStore.QueueDeliveredMessageForIndexingSql, "hm_message_search_queue");
    }
}
