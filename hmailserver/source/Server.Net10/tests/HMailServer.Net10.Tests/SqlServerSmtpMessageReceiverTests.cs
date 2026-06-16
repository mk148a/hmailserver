using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSmtpMessageReceiverTests
{
    [TestMethod]
    public void ReceiveSql_InsertsLockedQueueMessageRecipientsAndUnlocks()
    {
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertQueuedMessageSql, "INSERT INTO hm_messages");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertQueuedMessageSql, "OUTPUT INSERTED.messageid");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertQueuedMessageSql, "messagetype");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertQueuedMessageSql, "1,");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertQueuedMessageSql, "messagelocked");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertQueuedMessageSql, "messageuid");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertRecipientSql, "INSERT INTO hm_messagerecipients");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertRecipientSql, "recipientlocalaccountid");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertRecipientSql, "@LocalAccountId");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.UnlockQueuedMessageSql, "SET messagelocked = 0");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.UnlockQueuedMessageSql, "messagetype = 1");
    }
}
