using System.Text;
using HMailServer.Core.Abstractions;
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

    [TestMethod]
    public async Task ReceiveAsync_ReturnsSuccessWithoutQueueWriteWhenRuleProcessorDropsMessage()
    {
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            new FakeRuleProcessor(SmtpRuleProcessingResult.Drop(Encoding.Latin1.GetBytes("Subject: Drop\r\n\r\nBody\r\n"))));
        var request = new SmtpReceiveRequest(
            HeloHost: "client.example",
            IsExtendedSmtp: true,
            MailFrom: "sender@example.test",
            Recipients:
            [
                new SmtpResolvedRecipient(
                    "recipient@example.test",
                    "recipient@example.test",
                    LocalAccountId: 0,
                    IsLocal: false)
            ],
            DeclaredSize: null,
            MessageData: Encoding.Latin1.GetBytes("Subject: Drop\r\n\r\nBody\r\n"),
            ReceivedUtc: DateTimeOffset.UtcNow);

        var result = await receiver.ReceiveAsync(request, CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.IsNull(result.FailureResponse);
    }

    private sealed class FakeRuleProcessor : ISmtpRuleProcessor
    {
        private readonly SmtpRuleProcessingResult _result;

        public FakeRuleProcessor(SmtpRuleProcessingResult result)
        {
            _result = result;
        }

        public ValueTask<SmtpRuleProcessingResult> ProcessAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_result);
    }
}
