using System.Data;
using System.Reflection;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;
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
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedMessageSql, "messageruleforcedrouteid");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedMessageSql, "messagerulebindaddress");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedRecipientsSql, "FROM hm_messagerecipients");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedRecipientsSql, "recipientlocalaccountid");
        StringAssert.Contains(SqlServerDeliveryQueueMessageStore.SelectQueuedRecipientsSql, "ORDER BY recipientid ASC");
    }

    [TestMethod]
    public void UpdateMessageSizeSql_UsesExactLeasePredicateAndTypedParameters()
    {
        var message = new DeliveryQueuedMessage(
            new MessageIdentity(42, 1, 2, 3),
            "queue.eml",
            "sender@example.test",
            123,
            DateTimeOffset.UtcNow,
            0,
            0,
            []);
        using var connection = new SqlConnection();
        var createCommand = typeof(SqlServerDeliveryQueueMessageStore).GetMethod(
            "CreateUpdateSizeCommand",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(createCommand);
        using var command = (SqlCommand)createCommand.Invoke(
            null,
            [connection, message, 456L, "worker-a"])!;

        Assert.AreEqual(
            "UPDATE hm_messages SET messagesize=@MessageSize\nWHERE messageid=@MessageId AND messagetype=1 AND messagelocked=1 AND messageleaseowner=@LeaseOwner",
            command.CommandText);
        Assert.AreEqual(3, command.Parameters.Count);
        Assert.AreEqual(SqlDbType.BigInt, command.Parameters["@MessageSize"].SqlDbType);
        Assert.AreEqual(SqlDbType.BigInt, command.Parameters["@MessageId"].SqlDbType);
        Assert.AreEqual(SqlDbType.NVarChar, command.Parameters["@LeaseOwner"].SqlDbType);
        Assert.AreEqual(128, command.Parameters["@LeaseOwner"].Size);
        Assert.AreEqual(456L, command.Parameters["@MessageSize"].Value);
        Assert.AreEqual(42L, command.Parameters["@MessageId"].Value);
        Assert.AreEqual("worker-a", command.Parameters["@LeaseOwner"].Value);
    }
}
