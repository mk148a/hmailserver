using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDeliveryQueueMessageStoreTests
{
    [TestMethod]
    public void QueueMessageSql_LoadsOnlyMessagesLeasedByCurrentWorker()
    {
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedMessageSql, "messagetype = 1");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedMessageSql, "messagelocked = 1");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedMessageSql, "messageleaseowner = @LeaseOwner");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedMessageSql, "messagecreatetime");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedMessageSql, "messageflags");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedMessageSql, "messagecurnooftries");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedRecipientsSql, "FROM hm_messagerecipients");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedRecipientsSql, "recipientlocalaccountid");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedRecipientsSql, "ORDER BY recipientid ASC");
    }
}
