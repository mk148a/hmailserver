using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;

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
    public async Task ReceiveAsync_EnqueuesPrimaryMessageThroughDeliveryWakeBoundary()
    {
        var durableWriter = new RecordingSmtpQueueWriter();
        var wakeSignal = new RecordingDeliveryQueueWakeSignal();
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            queueWriter: new SignalingSmtpQueueWriter(durableWriter, wakeSignal));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Primary\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(1, durableWriter.Requests.Count);
        Assert.AreEqual("sender@example.test", durableWriter.Requests.Single().MailFrom);
        Assert.AreEqual(1, wakeSignal.SignalCount);
    }

    [TestMethod]
    public async Task ReceiveAsync_EnqueuesGeneratedAndPrimaryMessagesThroughDeliveryWakeBoundary()
    {
        var generatedMessage = new SmtpRuleGeneratedMessage(
            "generated@example.test",
            [
                new SmtpResolvedRecipient(
                    "generated-recipient@example.test",
                    "generated-recipient@example.test",
                    LocalAccountId: 0,
                    IsLocal: false)
            ],
            "Subject: Generated\r\n\r\nBody\r\n"u8.ToArray(),
            SpamFlagged: true);
        var durableWriter = new RecordingSmtpQueueWriter();
        var wakeSignal = new RecordingDeliveryQueueWakeSignal();
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            ruleProcessor: new FakeRuleProcessor(request =>
                SmtpRuleProcessingResult.Continue(
                    request.MessageData,
                    moveToImapFolder: null,
                    generatedMessages: [generatedMessage])),
            queueWriter: new SignalingSmtpQueueWriter(durableWriter, wakeSignal));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Primary\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        CollectionAssert.AreEqual(
            new[] { "generated@example.test", "sender@example.test" },
            durableWriter.Requests.Select(static request => request.MailFrom).ToArray());
        Assert.AreEqual(
            (byte)(SmtpQueueWriteRequest.RecentFlag | SmtpQueueWriteRequest.SpamFlag),
            durableWriter.Requests[0].MessageFlags);
        Assert.AreEqual(SmtpQueueWriteRequest.RecentFlag, durableWriter.Requests[1].MessageFlags);
        Assert.AreEqual(2, wakeSignal.SignalCount);
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
    public async Task ReceiveAsync_RejectsDnsBlockListHitBeforeScriptSpamAndAntivirus()
    {
        var eventCalled = false;
        var spamScanner = new FakeSpamScanner(
            MessageSpamScanResult.Clean("Subject: Spam\r\n\r\nBody\r\n"u8.ToArray()));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Clean());
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            eventScriptExecutor: new FakeEventScriptExecutor(
                _ =>
                {
                    eventCalled = true;
                    return SmtpRuleScriptExecutionResult.Continue();
                }),
            antivirusScanner: antivirusScanner,
            spamScanner: spamScanner,
            dnsBlockListChecker: new FakeDnsBlockListChecker(
                SmtpDnsBlockListResult.Blocked(
                    "zen.example.test",
                    "5.2.0.192.zen.example.test",
                    "127.0.0.2",
                    "554 Listed by DNSBL")));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Blocked\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Listed by DNSBL", result.FailureResponse);
        Assert.IsFalse(eventCalled);
        Assert.AreEqual(0, spamScanner.ScannedMessages.Count);
        Assert.AreEqual(0, antivirusScanner.ScannedMessages.Count);
    }

    [TestMethod]
    public async Task ReceiveAsync_RejectsReverseDnsFailureBeforeScriptSpamAndAntivirus()
    {
        var eventCalled = false;
        var spamScanner = new FakeSpamScanner(
            MessageSpamScanResult.Clean("Subject: Spam\r\n\r\nBody\r\n"u8.ToArray()));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Clean());
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            eventScriptExecutor: new FakeEventScriptExecutor(
                _ =>
                {
                    eventCalled = true;
                    return SmtpRuleScriptExecutionResult.Continue();
                }),
            antivirusScanner: antivirusScanner,
            spamScanner: spamScanner,
            reverseDnsChecker: new FakeReverseDnsChecker(
                SmtpReverseDnsResult.Reject(
                    "192.0.2.5",
                    Array.Empty<string>(),
                    "missing-ptr",
                    "554 Missing PTR")));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Blocked\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Missing PTR", result.FailureResponse);
        Assert.IsFalse(eventCalled);
        Assert.AreEqual(0, spamScanner.ScannedMessages.Count);
        Assert.AreEqual(0, antivirusScanner.ScannedMessages.Count);
    }

    [TestMethod]
    public async Task ReceiveAsync_RejectsSenderDomainMxFailureBeforeScriptSpamAndAntivirus()
    {
        var eventCalled = false;
        var spamScanner = new FakeSpamScanner(
            MessageSpamScanResult.Clean("Subject: Spam\r\n\r\nBody\r\n"u8.ToArray()));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Clean());
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            eventScriptExecutor: new FakeEventScriptExecutor(
                _ =>
                {
                    eventCalled = true;
                    return SmtpRuleScriptExecutionResult.Continue();
                }),
            antivirusScanner: antivirusScanner,
            spamScanner: spamScanner,
            senderDomainMxChecker: new FakeSenderDomainMxChecker(
                SmtpSenderDomainMxResult.Reject(
                    "example.test",
                    "missing-mx",
                    "554 Sender domain missing MX")));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Blocked\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Sender domain missing MX", result.FailureResponse);
        Assert.IsFalse(eventCalled);
        Assert.AreEqual(0, spamScanner.ScannedMessages.Count);
        Assert.AreEqual(0, antivirusScanner.ScannedMessages.Count);
    }

    [TestMethod]
    public async Task ReceiveAsync_DefersGreylistedMessageBeforeScriptSpamAndAntivirus()
    {
        var eventCalled = false;
        var spamScanner = new FakeSpamScanner(
            MessageSpamScanResult.Clean("Subject: Spam\r\n\r\nBody\r\n"u8.ToArray()));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Clean());
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            eventScriptExecutor: new FakeEventScriptExecutor(
                _ =>
                {
                    eventCalled = true;
                    return SmtpRuleScriptExecutionResult.Continue();
                }),
            antivirusScanner: antivirusScanner,
            spamScanner: spamScanner,
            greylistingChecker: new FakeGreylistingChecker(
                SmtpGreylistingResult.Defer(
                    "recipient@example.test",
                    "451 Please try again later.")));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Greylisted\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("451 Please try again later.", result.FailureResponse);
        Assert.IsFalse(eventCalled);
        Assert.AreEqual(0, spamScanner.ScannedMessages.Count);
        Assert.AreEqual(0, antivirusScanner.ScannedMessages.Count);
    }

    [TestMethod]
    public async Task ReceiveAsync_DoesNotBypassGreylistingOnSpfPassByDefault()
    {
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("Should-Not-Scan"));
        var greylistingChecker = new FakeGreylistingChecker(
            SmtpGreylistingResult.Defer(
                "recipient@example.test",
                "451 Please try again later."));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            spfPolicy: new FakeSpfPolicy(CreateSpfPolicyResult(SmtpSpfPolicyStatus.Pass)),
            greylistingChecker: greylistingChecker);

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: SPF Pass\r\n\r\nBody\r\n"u8.ToArray()) with { IsAuthenticated = false },
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("451 Please try again later.", result.FailureResponse);
        Assert.AreEqual(1, greylistingChecker.Requests.Count);
        Assert.AreEqual(0, antivirusScanner.ScannedMessages.Count);
    }

    [TestMethod]
    public async Task ReceiveAsync_BypassesGreylistingOnSpfPassWhenConfigured()
    {
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("After-Spf-Pass-Bypass"));
        var greylistingChecker = new FakeGreylistingChecker(
            SmtpGreylistingResult.Defer(
                "recipient@example.test",
                "451 Please try again later."));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            spfPolicy: new FakeSpfPolicy(CreateSpfPolicyResult(SmtpSpfPolicyStatus.Pass)),
            greylistingChecker: greylistingChecker,
            greylistingOptions: new SmtpGreylistingOptions { BypassOnSpfPass = true });

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: SPF Pass\r\n\r\nBody\r\n"u8.ToArray()) with { IsAuthenticated = false },
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Virus detected: After-Spf-Pass-Bypass", result.FailureResponse);
        Assert.AreEqual(0, greylistingChecker.Requests.Count);
        Assert.AreEqual(1, antivirusScanner.ScannedMessages.Count);
    }

    [TestMethod]
    public async Task ReceiveAsync_DoesNotBypassGreylistingForNonPassSpfResults()
    {
        var nonPassingStatuses = new[]
        {
            SmtpSpfPolicyStatus.Fail,
            SmtpSpfPolicyStatus.None,
            SmtpSpfPolicyStatus.Neutral,
            SmtpSpfPolicyStatus.SoftFail,
            SmtpSpfPolicyStatus.TempError,
            SmtpSpfPolicyStatus.PermError,
            SmtpSpfPolicyStatus.Skipped
        };

        foreach (var status in nonPassingStatuses)
        {
            var greylistingChecker = new FakeGreylistingChecker(
                SmtpGreylistingResult.Defer(
                    "recipient@example.test",
                    "451 Please try again later."));
            var antivirusScanner = new FakeAntivirusScanner(
                MessageAntivirusScanResult.Infected("Should-Not-Scan"));
            var receiver = new SqlServerSmtpMessageReceiver(
                new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
                new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
                antivirusScanner: antivirusScanner,
                spfPolicy: new FakeSpfPolicy(CreateSpfPolicyResult(status)),
                greylistingChecker: greylistingChecker,
                greylistingOptions: new SmtpGreylistingOptions { BypassOnSpfPass = true });

            var result = await receiver.ReceiveAsync(
                CreateRequest("Subject: SPF NonPass\r\n\r\nBody\r\n"u8.ToArray()) with { IsAuthenticated = false },
                CancellationToken.None);

            Assert.IsFalse(result.Accepted, status.ToString());
            Assert.AreEqual("451 Please try again later.", result.FailureResponse, status.ToString());
            Assert.AreEqual(1, greylistingChecker.Requests.Count, status.ToString());
            Assert.AreEqual(0, antivirusScanner.ScannedMessages.Count, status.ToString());
        }
    }

    [TestMethod]
    public async Task ReceiveAsync_MarksSpfFailAsSpamWithoutRejectingMessage()
    {
        var statusRuntimeState = new ServerStatusRuntimeState();
        var spfPolicy = new FakeSpfPolicy(
            SmtpSpfPolicyResult.FromEvaluation(
                SmtpSpfPolicyStatus.Fail,
                failScore: 3,
                domain: "example.test",
                sender: "sender@example.test",
                heloDomain: "client.example",
                matchedMechanism: "-all",
                diagnostic: "Blocked by SPF."));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("After-Spf-Test"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            spfPolicy: spfPolicy,
            statusRuntimeState: statusRuntimeState);

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: SPF\r\n\r\nBody\r\n"u8.ToArray()) with { IsAuthenticated = false },
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Virus detected: After-Spf-Test", result.FailureResponse);
        Assert.AreEqual(1, spfPolicy.Requests.Count);
        Assert.AreEqual(1, antivirusScanner.ScannedMessages.Count);
        Assert.AreEqual(1, statusRuntimeState.Capture().RemovedSpamMessages);
    }

    [TestMethod]
    public async Task ReceiveAsync_MarksDkimPermFailAsSpamWithoutRejectingMessage()
    {
        var statusRuntimeState = new ServerStatusRuntimeState();
        var dkimPolicy = new FakeDkimPolicy(
            SmtpDkimPolicyResult.FromEvaluation(
                SmtpDkimPolicyStatus.PermFail,
                failureScore: 5,
                diagnostic: "Rejected by DKIM."));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("After-Dkim-Test"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            dkimPolicy: dkimPolicy,
            statusRuntimeState: statusRuntimeState);

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: DKIM\r\n\r\nBody\r\n"u8.ToArray()) with { IsAuthenticated = false },
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Virus detected: After-Dkim-Test", result.FailureResponse);
        Assert.AreEqual(1, dkimPolicy.Requests.Count);
        Assert.AreEqual(1, antivirusScanner.ScannedMessages.Count);
        Assert.AreEqual(1, statusRuntimeState.Capture().RemovedSpamMessages);
    }

    [TestMethod]
    public async Task ReceiveAsync_DoesNotMarkNonPermFailDkimResultsAsSpam()
    {
        var statuses = new[]
        {
            SmtpDkimPolicyStatus.Pass,
            SmtpDkimPolicyStatus.Neutral,
            SmtpDkimPolicyStatus.TempFail,
            SmtpDkimPolicyStatus.Skipped
        };

        foreach (var status in statuses)
        {
            var statusRuntimeState = new ServerStatusRuntimeState();
            var dkimPolicy = new FakeDkimPolicy(CreateDkimPolicyResult(status));
            var antivirusScanner = new FakeAntivirusScanner(
                MessageAntivirusScanResult.Infected("After-Dkim-NonPermFail"));
            var receiver = new SqlServerSmtpMessageReceiver(
                new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
                new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
                antivirusScanner: antivirusScanner,
                dkimPolicy: dkimPolicy,
                statusRuntimeState: statusRuntimeState);

            var result = await receiver.ReceiveAsync(
                CreateRequest("Subject: DKIM\r\n\r\nBody\r\n"u8.ToArray()) with { IsAuthenticated = false },
                CancellationToken.None);

            Assert.IsFalse(result.Accepted, status.ToString());
            Assert.AreEqual("554 Virus detected: After-Dkim-NonPermFail", result.FailureResponse, status.ToString());
            Assert.AreEqual(1, dkimPolicy.Requests.Count, status.ToString());
            Assert.AreEqual(1, antivirusScanner.ScannedMessages.Count, status.ToString());
            Assert.AreEqual(0, statusRuntimeState.Capture().RemovedSpamMessages, status.ToString());
        }
    }

    [TestMethod]
    public async Task ReceiveAsync_PassesSpfAndDkimResultsToDmarcPolicy()
    {
        var spfPolicy = new FakeSpfPolicy(CreateSpfPolicyResult(SmtpSpfPolicyStatus.Pass));
        var dkimPolicy = new FakeDkimPolicy(
            SmtpDkimPolicyResult.FromEvaluation(
                SmtpDkimPolicyStatus.Pass,
                failureScore: 5,
                diagnostic: "DKIM pass.",
                passingDomains: ["example.test"]));
        var dmarcPolicy = new FakeDmarcPolicy(
            SmtpDmarcPolicyResult.FromEvaluation(
                SmtpDmarcPolicyStatus.Pass,
                SmtpDmarcAppliedPolicy.None,
                markFailuresAsSpam: false,
                failureScore: 0,
                headerFromDomain: "example.test",
                diagnostic: "DMARC pass."));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("After-Dmarc-Test"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            spfPolicy: spfPolicy,
            dkimPolicy: dkimPolicy,
            dmarcPolicy: dmarcPolicy);

        var result = await receiver.ReceiveAsync(
            CreateRequest("From: Sender <sender@example.test>\r\nSubject: DMARC\r\n\r\nBody\r\n"u8.ToArray()) with { IsAuthenticated = false },
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Virus detected: After-Dmarc-Test", result.FailureResponse);
        Assert.AreEqual(1, spfPolicy.Requests.Count);
        Assert.AreEqual(1, dkimPolicy.Requests.Count);
        Assert.AreEqual(1, dmarcPolicy.Requests.Count);
        Assert.IsTrue(dmarcPolicy.Requests[0].SpfPolicyResult.Passed);
        CollectionAssert.AreEqual(
            new[] { "example.test" },
            dmarcPolicy.Requests[0].DkimPolicyResult.PassingDomains.ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_PassesSpfDkimAndDmarcSpamClassificationToGlobalRules()
    {
        SmtpReceiveRequest? capturedRuleRequest = null;
        var spfPolicy = new FakeSpfPolicy(
            SmtpSpfPolicyResult.FromEvaluation(
                SmtpSpfPolicyStatus.Fail,
                failScore: 3,
                domain: "example.test",
                sender: "sender@example.test",
                heloDomain: "client.example",
                matchedMechanism: "-all",
                diagnostic: "SPF fail."));
        var dkimPolicy = new FakeDkimPolicy(
            SmtpDkimPolicyResult.FromEvaluation(
                SmtpDkimPolicyStatus.PermFail,
                failureScore: 5,
                diagnostic: "DKIM fail."));
        var dmarcPolicy = new FakeDmarcPolicy(
            SmtpDmarcPolicyResult.FromEvaluation(
                SmtpDmarcPolicyStatus.Fail,
                SmtpDmarcAppliedPolicy.Reject,
                markFailuresAsSpam: true,
                failureScore: 6,
                headerFromDomain: "example.test",
                diagnostic: "DMARC fail."));
        var queueWriter = new RecordingSmtpQueueWriter();
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            ruleProcessor: new FakeRuleProcessor(
                request =>
                {
                    capturedRuleRequest = request;
                    return SmtpRuleProcessingResult.Continue(request.MessageData);
                }),
            queueWriter: queueWriter,
            spfPolicy: spfPolicy,
            dkimPolicy: dkimPolicy,
            dmarcPolicy: dmarcPolicy);

        var result = await receiver.ReceiveAsync(
            CreateRequest("From: Sender <sender@example.test>\r\nSubject: Classification\r\n\r\nBody\r\n"u8.ToArray()) with { IsAuthenticated = false },
            CancellationToken.None);

        Assert.IsTrue(result.Accepted, result.FailureResponse);
        Assert.IsNotNull(capturedRuleRequest);
        Assert.IsTrue(capturedRuleRequest.OriginalMessageSpamFlagged);
        Assert.AreEqual(1, spfPolicy.Requests.Count);
        Assert.AreEqual(1, dkimPolicy.Requests.Count);
        Assert.AreEqual(1, dmarcPolicy.Requests.Count);
        Assert.AreEqual(
            (byte)(SmtpQueueWriteRequest.RecentFlag | SmtpQueueWriteRequest.SpamFlag),
            queueWriter.Requests.Single().MessageFlags);
    }

    [TestMethod]
    public async Task ReceiveAsync_MarksDmarcPolicyFailureAsSpamWithoutRejectingMessage()
    {
        var statusRuntimeState = new ServerStatusRuntimeState();
        var dmarcPolicy = new FakeDmarcPolicy(
            SmtpDmarcPolicyResult.FromEvaluation(
                SmtpDmarcPolicyStatus.Fail,
                SmtpDmarcAppliedPolicy.Reject,
                markFailuresAsSpam: true,
                failureScore: 6,
                headerFromDomain: "example.test",
                diagnostic: "Rejected by DMARC."));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("After-Dmarc-Failure"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            dmarcPolicy: dmarcPolicy,
            statusRuntimeState: statusRuntimeState);

        var result = await receiver.ReceiveAsync(
            CreateRequest("From: Sender <sender@example.test>\r\nSubject: DMARC\r\n\r\nBody\r\n"u8.ToArray()) with { IsAuthenticated = false },
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Virus detected: After-Dmarc-Failure", result.FailureResponse);
        Assert.AreEqual(1, dmarcPolicy.Requests.Count);
        Assert.AreEqual(1, antivirusScanner.ScannedMessages.Count);
        Assert.AreEqual(1, statusRuntimeState.Capture().RemovedSpamMessages);
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
    public async Task ReceiveAsync_PassesSpamClassificationToGlobalRuleProcessor()
    {
        var spamProcessedMessage = Encoding.Latin1.GetBytes(
            "Subject: Original\r\n\r\nBody\r\n");
        var spamScanner = new FakeSpamScanner(
            MessageSpamScanResult.Spam(
                spamProcessedMessage,
                score: 7,
                details: "Tagged as spam"));
        SmtpReceiveRequest? capturedRuleRequest = null;
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            ruleProcessor: new FakeRuleProcessor(
                request =>
                {
                    capturedRuleRequest = request;
                    return SmtpRuleProcessingResult.Drop(request.MessageData);
                }),
            spamScanner: spamScanner,
            spamPolicy: new MessageSpamPolicy(
                new MessageSpamPolicyOptions
                {
                    AddSpamHeader = true
                }));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Original\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsTrue(result.Accepted, result.FailureResponse);
        Assert.IsNotNull(capturedRuleRequest);
        Assert.IsTrue(capturedRuleRequest.OriginalMessageSpamFlagged);
        StringAssert.Contains(
            Encoding.Latin1.GetString(capturedRuleRequest.MessageData),
            "X-hMailServer-Spam: YES");
        Assert.AreEqual(1, spamScanner.ScannedMessages.Count);
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

    [TestMethod]
    public async Task ReceiveAsync_PassesSpamProcessedMessageToAntivirusScan()
    {
        var spamProcessedMessage = Encoding.Latin1.GetBytes(
            "X-Spam-Status: Yes, score=7.1 required=5.0\r\nSubject: Spam\r\n\r\nBody\r\n");
        var spamScanner = new FakeSpamScanner(
            MessageSpamScanResult.Spam(spamProcessedMessage, score: 7));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("After-Spam-Test"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            spamScanner: spamScanner);

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Original\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Virus detected: After-Spam-Test", result.FailureResponse);
        Assert.AreEqual(1, spamScanner.ScannedMessages.Count);
        StringAssert.Contains(
            Encoding.Latin1.GetString(antivirusScanner.ScannedMessages.Single()),
            "X-Spam-Status: Yes");
    }

    [TestMethod]
    public async Task ReceiveAsync_AppliesAttachmentPolicyBeforeAntivirusScan()
    {
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("After-Attachment-Policy-Test"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            attachmentPolicy: new MimeMessageAttachmentPolicy(
                new MessageAttachmentPolicyOptions
                {
                    Enabled = true,
                    BlockedWildcards = [".exe"],
                    ReplacementTextTemplate = "Blocked: %MACRO_FILE%"
                }));

        var result = await receiver.ReceiveAsync(
            CreateRequest(CreateMessageWithAttachment("evil.exe", "MZ")),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Virus detected: After-Attachment-Policy-Test", result.FailureResponse);
        var attachment = (TextPart)GetSingleAttachment(antivirusScanner.ScannedMessages.Single());
        Assert.AreEqual("evil.exe.txt", attachment.FileName);
        Assert.AreEqual("Blocked: evil.exe", attachment.Text);
    }

    [TestMethod]
    public async Task ReceiveAsync_RejectsUrlBlockListHitBeforeAntivirusScan()
    {
        var spamScanner = new FakeSpamScanner(
            MessageSpamScanResult.Clean("Subject: Spam\r\n\r\nBody http://bad.example/\r\n"u8.ToArray()));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("Should-Not-Scan"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            spamScanner: spamScanner,
            urlBlockListChecker: new FakeUrlBlockListChecker(
                SmtpUrlBlockListResult.Blocked(
                    "multi.surbl.test",
                    "bad.example",
                    "bad.example.multi.surbl.test",
                    "127.0.0.2",
                    "554 Listed by SURBL")));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Original\r\n\r\nBody http://bad.example/\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Listed by SURBL", result.FailureResponse);
        Assert.AreEqual(1, spamScanner.ScannedMessages.Count);
        Assert.AreEqual(0, antivirusScanner.ScannedMessages.Count);
    }

    [TestMethod]
    public async Task ReceiveAsync_AppliesSpamPolicyHeadersBeforeAntivirusScan()
    {
        var spamProcessedMessage = Encoding.Latin1.GetBytes(
            "Subject: Original\r\nX-hMailServer-Reason-Score: 1\r\n\r\nBody\r\n");
        var spamScanner = new FakeSpamScanner(
            MessageSpamScanResult.Spam(
                spamProcessedMessage,
                score: 7,
                details: "Tagged as Spam by SpamAssassin"));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("After-Spam-Policy-Test"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            spamScanner: spamScanner,
            spamPolicy: new MessageSpamPolicy(
                new MessageSpamPolicyOptions
                {
                    AddSpamHeader = true,
                    AddReasonHeaders = true,
                    PrependSubject = true,
                    SubjectPrefix = "[SPAM]"
                }));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Original\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        var scannedMessage = Encoding.Latin1.GetString(antivirusScanner.ScannedMessages.Single());
        StringAssert.Contains(scannedMessage, "Subject: [SPAM] Original\r\n");
        StringAssert.Contains(scannedMessage, "X-hMailServer-Spam: YES\r\n");
        StringAssert.Contains(
            scannedMessage,
            "X-hMailServer-Reason-1: Tagged as Spam by SpamAssassin - (Score: 7)\r\n");
        StringAssert.Contains(scannedMessage, "X-hMailServer-Reason-Score: 7\r\n");
        Assert.IsFalse(scannedMessage.Contains("X-hMailServer-Reason-Score: 1", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ReceiveAsync_RejectsSpamBeforeAntivirusWhenDeleteThresholdMatches()
    {
        var spamProcessedMessage = Encoding.Latin1.GetBytes("Subject: Spam\r\n\r\nBody\r\n");
        var spamScanner = new FakeSpamScanner(
            MessageSpamScanResult.Clean(
                spamProcessedMessage,
                details: "Score delete threshold",
                score: 10));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("Should-Not-Scan"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            spamScanner: spamScanner,
            spamPolicy: new MessageSpamPolicy(
                new MessageSpamPolicyOptions
                {
                    SpamDeleteThreshold = 10
                }));

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Original\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("554 Score delete threshold", result.FailureResponse);
        Assert.AreEqual(1, spamScanner.ScannedMessages.Count);
        Assert.AreEqual(0, antivirusScanner.ScannedMessages.Count);
    }

    [TestMethod]
    public async Task ReceiveAsync_PreservesOriginalMessageWhenSpamScannerThrows()
    {
        var spamScanner = new FakeSpamScanner(
            _ => throw new IOException("spamd failed"));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("Original-Test"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            spamScanner: spamScanner);

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Original\r\n\r\nBody\r\n"u8.ToArray()),
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        StringAssert.Contains(
            Encoding.Latin1.GetString(antivirusScanner.ScannedMessages.Single()),
            "Subject: Original");
        Assert.IsFalse(
            Encoding.Latin1.GetString(antivirusScanner.ScannedMessages.Single())
                .Contains("X-Spam-Status", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ReceiveAsync_SkipsSpamScannerWhenRequestDisablesIt()
    {
        var spamProcessedMessage = Encoding.Latin1.GetBytes("X-Spam-Status: Yes\r\nSubject: Spam\r\n\r\nBody\r\n");
        var spamScanner = new FakeSpamScanner(
            MessageSpamScanResult.Spam(spamProcessedMessage, score: 5));
        var antivirusScanner = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("No-Spam-Scan-Test"));
        var receiver = new SqlServerSmtpMessageReceiver(
            new SqlServerConnectionFactory("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true"),
            new MessageFilePathResolver(new MessageFileSearchDocumentSourceOptions(Path.GetTempPath())),
            antivirusScanner: antivirusScanner,
            spamScanner: spamScanner);

        var result = await receiver.ReceiveAsync(
            CreateRequest("Subject: Original\r\n\r\nBody\r\n"u8.ToArray()) with { EnableSpamScan = false },
            CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(0, spamScanner.ScannedMessages.Count);
        StringAssert.Contains(
            Encoding.Latin1.GetString(antivirusScanner.ScannedMessages.Single()),
            "Subject: Original");
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

    private static SmtpSpfPolicyResult CreateSpfPolicyResult(SmtpSpfPolicyStatus status) =>
        status == SmtpSpfPolicyStatus.Skipped
            ? SmtpSpfPolicyResult.Skipped
            : SmtpSpfPolicyResult.FromEvaluation(
                status,
                failScore: 3,
                domain: "example.test",
                sender: "sender@example.test",
                heloDomain: "client.example",
                matchedMechanism: status == SmtpSpfPolicyStatus.Pass ? "+all" : null,
                diagnostic: status.ToString());

    private static SmtpDkimPolicyResult CreateDkimPolicyResult(SmtpDkimPolicyStatus status) =>
        status == SmtpDkimPolicyStatus.Skipped
            ? SmtpDkimPolicyResult.Skipped
            : SmtpDkimPolicyResult.FromEvaluation(
                status,
                failureScore: 5,
                diagnostic: status.ToString());

    private static byte[] CreateMessageWithAttachment(string fileName, string content)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.test"));
        message.To.Add(MailboxAddress.Parse("recipient@example.test"));
        message.Subject = "Attachment";

        var multipart = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "Body" },
            new MimePart("application", "octet-stream")
            {
                FileName = fileName,
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                Content = new MimeContent(new MemoryStream(Encoding.ASCII.GetBytes(content)))
            }
        };
        message.Body = multipart;

        using var output = new MemoryStream();
        message.WriteTo(output);
        return output.ToArray();
    }

    private static MimeEntity GetSingleAttachment(byte[] messageData)
    {
        using var input = new MemoryStream(messageData, writable: false);
        var message = MimeMessage.Load(input);
        return message.Attachments.Single();
    }

    private sealed class RecordingSmtpQueueWriter : ISmtpQueueWriter
    {
        public List<SmtpQueueWriteRequest> Requests { get; } = [];

        public ValueTask EnqueueAsync(
            SmtpQueueWriteRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingDeliveryQueueWakeSignal : IDeliveryQueueWakeSignal
    {
        public int SignalCount { get; private set; }

        public void Signal() => SignalCount++;

        public ValueTask<bool> WaitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

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

    private sealed class FakeSpamScanner : IMessageSpamScanner
    {
        private readonly Func<ReadOnlyMemory<byte>, MessageSpamScanResult> _scan;

        public FakeSpamScanner(MessageSpamScanResult result)
            : this(_ => result)
        {
        }

        public FakeSpamScanner(Func<ReadOnlyMemory<byte>, MessageSpamScanResult> scan)
        {
            _scan = scan;
        }

        public List<byte[]> ScannedMessages { get; } = [];

        public ValueTask<MessageSpamScanResult> ScanAsync(
            ReadOnlyMemory<byte> messageData,
            string envelopeFrom,
            CancellationToken cancellationToken)
        {
            ScannedMessages.Add(messageData.ToArray());
            return ValueTask.FromResult(_scan(messageData));
        }
    }

    private sealed class FakeDnsBlockListChecker : ISmtpDnsBlockListChecker
    {
        private readonly SmtpDnsBlockListResult _result;

        public FakeDnsBlockListChecker(SmtpDnsBlockListResult result)
        {
            _result = result;
        }

        public ValueTask<SmtpDnsBlockListResult> CheckAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_result);
    }

    private sealed class FakeReverseDnsChecker : ISmtpReverseDnsChecker
    {
        private readonly SmtpReverseDnsResult _result;

        public FakeReverseDnsChecker(SmtpReverseDnsResult result)
        {
            _result = result;
        }

        public ValueTask<SmtpReverseDnsResult> CheckAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_result);
    }

    private sealed class FakeSenderDomainMxChecker : ISmtpSenderDomainMxChecker
    {
        private readonly SmtpSenderDomainMxResult _result;

        public FakeSenderDomainMxChecker(SmtpSenderDomainMxResult result)
        {
            _result = result;
        }

        public ValueTask<SmtpSenderDomainMxResult> CheckAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_result);
    }

    private sealed class FakeGreylistingChecker : ISmtpGreylistingChecker
    {
        private readonly SmtpGreylistingResult _result;

        public FakeGreylistingChecker(SmtpGreylistingResult result)
        {
            _result = result;
        }

        public List<SmtpReceiveRequest> Requests { get; } = [];

        public ValueTask<SmtpGreylistingResult> CheckAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class FakeSpfPolicy : ISmtpSpfPolicy
    {
        private readonly SmtpSpfPolicyResult _result;

        public FakeSpfPolicy(SmtpSpfPolicyResult result)
        {
            _result = result;
        }

        public List<SmtpReceiveRequest> Requests { get; } = [];

        public ValueTask<SmtpSpfPolicyResult> CheckAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class FakeDkimPolicy : ISmtpDkimPolicy
    {
        private readonly SmtpDkimPolicyResult _result;

        public FakeDkimPolicy(SmtpDkimPolicyResult result)
        {
            _result = result;
        }

        public List<SmtpReceiveRequest> Requests { get; } = [];

        public ValueTask<SmtpDkimPolicyResult> CheckAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class FakeDmarcPolicy : ISmtpDmarcPolicy
    {
        private readonly SmtpDmarcPolicyResult _result;

        public FakeDmarcPolicy(SmtpDmarcPolicyResult result)
        {
            _result = result;
        }

        public List<DmarcPolicyRequest> Requests { get; } = [];

        public ValueTask<SmtpDmarcPolicyResult> CheckAsync(
            SmtpReceiveRequest request,
            SmtpSpfPolicyResult spfPolicyResult,
            SmtpDkimPolicyResult dkimPolicyResult,
            CancellationToken cancellationToken)
        {
            Requests.Add(new DmarcPolicyRequest(request, spfPolicyResult, dkimPolicyResult));
            return ValueTask.FromResult(_result);
        }
    }

    private sealed record DmarcPolicyRequest(
        SmtpReceiveRequest Request,
        SmtpSpfPolicyResult SpfPolicyResult,
        SmtpDkimPolicyResult DkimPolicyResult);

    private sealed class FakeUrlBlockListChecker : ISmtpUrlBlockListChecker
    {
        private readonly SmtpUrlBlockListResult _result;

        public FakeUrlBlockListChecker(SmtpUrlBlockListResult result)
        {
            _result = result;
        }

        public ValueTask<SmtpUrlBlockListResult> CheckAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_result);
    }
}
