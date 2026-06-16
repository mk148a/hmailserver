using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerDeliveryQueueRecipientStoreTests
{
    [TestMethod]
    public void DeleteRecipientsSql_DeletesOnlyRecipientsOnLeasedQueueMessage()
    {
        StringAssert.Contains(SqlServerDeliveryQueueRecipientStore.DeleteRecipientsSqlTemplate, "DELETE FROM hm_messagerecipients");
        StringAssert.Contains(SqlServerDeliveryQueueRecipientStore.DeleteRecipientsSqlTemplate, "recipientmessageid = @MessageId");
        StringAssert.Contains(SqlServerDeliveryQueueRecipientStore.DeleteRecipientsSqlTemplate, "messagetype = 1");
        StringAssert.Contains(SqlServerDeliveryQueueRecipientStore.DeleteRecipientsSqlTemplate, "messagelocked = 1");
        StringAssert.Contains(SqlServerDeliveryQueueRecipientStore.DeleteRecipientsSqlTemplate, "messageleaseowner = @LeaseOwner");
    }
}
