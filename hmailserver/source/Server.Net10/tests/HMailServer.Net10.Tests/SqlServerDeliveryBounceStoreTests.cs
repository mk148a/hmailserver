using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDeliveryBounceStoreTests
{
    [TestMethod]
    public void BounceSql_QueuesUnlockedBounceMessageToOriginalSender()
    {
        StringAssert.Contains(SqlServerDeliveryBounceStore.InsertBounceMessageSql, "INSERT INTO hm_messages");
        StringAssert.Contains(SqlServerDeliveryBounceStore.InsertBounceMessageSql, "messagetype");
        StringAssert.Contains(SqlServerDeliveryBounceStore.InsertBounceRecipientSql, "INSERT INTO hm_messagerecipients");
        StringAssert.Contains(SqlServerDeliveryBounceStore.InsertBounceRecipientSql, "recipientlocalaccountid");
        StringAssert.Contains(SqlServerDeliveryBounceStore.UnlockBounceMessageSql, "SET messagelocked = 0");
    }
}
