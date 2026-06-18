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
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertQueuedMessageSql, "messageruleforcedrouteid");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertQueuedMessageSql, "@RuleForcedRouteId");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertQueuedMessageSql, "messagerulebindaddress");
        StringAssert.Contains(SqlServerSmtpMessageReceiver.InsertQueuedMessageSql, "@RuleBindAddress");
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

    [TestMethod]
    public async Task ReceiveAsync_ReturnsFailureBeforeRuleProcessingWhenAcceptEventRejects()
    {
        var ruleProcessorCalled = false;
        SmtpEventScriptExecutionRequest? capturedEventRequest = null;
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            new FakeRuleProcessor(
                request =>
                {
                    ruleProcessorCalled = true;
                    return SmtpRuleProcessingResult.Drop(request.MessageData);
                }),
            new FakeEventScriptExecutor(
                request =>
                {
                    capturedEventRequest = request;
                    return SmtpRuleScriptExecutionResult.Failure("554 blocked by event");
                }));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Reject\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 blocked by event", result.FailureResponse);
        Assert.IsFalse(ruleProcessorCalled);
        Assert.IsNotNull(capturedEventRequest);
        Assert.AreEqual("OnAcceptMessage", capturedEventRequest.EventName);
        Assert.AreEqual("client.example", capturedEventRequest.Client.HeloHost);
        Assert.AreEqual("user@example.test", capturedEventRequest.Client.Username);
    }

    [TestMethod]
    public async Task ReceiveAsync_PassesAcceptEventMutatedMessageToRuleProcessor()
    {
        var mutatedMessage = Encoding.Latin1.GetBytes("Subject: Mutated\r\nX-Event: yes\r\n\r\nBody\r\n");
        SmtpReceiveRequest? capturedRuleRequest = null;
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            new FakeRuleProcessor(
                request =>
                {
                    capturedRuleRequest = request;
                    return SmtpRuleProcessingResult.Drop(request.MessageData);
                }),
            new FakeEventScriptExecutor(
                _ => SmtpRuleScriptExecutionResult.Continue(mutatedMessage)));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Original\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsTrue(result.Accepted, result.FailureResponse);
        Assert.IsNotNull(capturedRuleRequest);
        StringAssert.Contains(
            Encoding.Latin1.GetString(capturedRuleRequest.MessageData),
            "X-Event: yes");
    }

    [TestMethod]
    public async Task ReceiveAsync_RejectsVirusBeforeQueueWrite()
    {
        var scanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("Eicar-Test-Signature"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: scanner);

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Virus\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Virus detected: Eicar-Test-Signature", result.FailureResponse);
        Assert.AreEqual(1, scanner.ScannedMessages.Count);
    }

    [TestMethod]
    public async Task ReceiveAsync_ScansAcceptEventMutatedMessage()
    {
        var mutatedMessage = Encoding.Latin1.GetBytes("Subject: Mutated\r\nX-Event: yes\r\n\r\nBody\r\n");
        var scanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("Mutated-Test"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            eventScriptExecutor: new FakeEventScriptExecutor(
                _ => SmtpRuleScriptExecutionResult.Continue(mutatedMessage)),
            antivirusScanner: scanner);

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Original\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Virus detected: Mutated-Test", result.FailureResponse);
        StringAssert.Contains(
            Encoding.Latin1.GetString(scanner.ScannedMessages.Single()),
            "X-Event: yes");
    }

    private static SmtpReceiveRequest CreateRequest(byte[] messageData) =>
        new(
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
            MessageData: messageData,
            ReceivedUtc: DateTimeOffset.UtcNow,
            ClientIPAddress: "127.0.0.1",
            ClientPort: 25,
            SessionId: 123,
            AuthenticatedUsername: "user@example.test",
            IsAuthenticated: true,
            IsEncryptedConnection: true);

    private sealed class FakeRuleProcessor : ISmtpRuleProcessor
    {
        private readonly Func<SmtpReceiveRequest, SmtpRuleProcessingResult> _process;

        public FakeRuleProcessor(SmtpRuleProcessingResult result)
        {
            _process = _ => result;
        }

        public FakeRuleProcessor(Func<SmtpReceiveRequest, SmtpRuleProcessingResult> process)
        {
            _process = process;
        }

        public ValueTask<SmtpRuleProcessingResult> ProcessAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_process(request));
    }

    private sealed class FakeEventScriptExecutor : ISmtpEventScriptExecutor
    {
        private readonly Func<SmtpEventScriptExecutionRequest, SmtpRuleScriptExecutionResult> _execute;

        public FakeEventScriptExecutor(Func<SmtpEventScriptExecutionRequest, SmtpRuleScriptExecutionResult> execute)
        {
            _execute = execute;
        }

        public SmtpRuleScriptExecutionResult Execute(
            SmtpEventScriptExecutionRequest request,
            CancellationToken cancellationToken) =>
            _execute(request);
    }

    private sealed class FakeAntivirusScanner : IMessageAntivirusScanner
    {
        private readonly MessageAntivirusScanResult _result;

        public FakeAntivirusScanner(MessageAntivirusScanResult result)
        {
            _result = result;
        }

        public List<byte[]> ScannedMessages { get; } = [];

        public ValueTask<MessageAntivirusScanResult> ScanAsync(
            ReadOnlyMemory<byte> messageData,
            CancellationToken cancellationToken)
        {
            ScannedMessages.Add(messageData.ToArray());
            return ValueTask.FromResult(_result);
        }
    }
}
