using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SqlServerSmtpRuleProcessorTests
{
    [TestMethod]
    public void ApplyRules_DropsMessageWhenDeleteActionMatches()
    {
        var request = CreateRequest("Subject: Block me\r\n\r\nBody\r\n");
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 10,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "block"),
            actions: new SmtpRuleAction(
                Id: 11,
                Type: SmtpRuleActionType.Delete,
                SortOrder: 1,
                ImapFolder: string.Empty,
                Subject: string.Empty,
                FromName: string.Empty,
                FromAddress: string.Empty,
                To: string.Empty,
                Body: string.Empty,
                FileName: string.Empty,
                ScriptFunction: string.Empty,
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false));

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.IsTrue(result.Accepted);
        Assert.IsTrue(result.DropMessage);
    }

    [TestMethod]
    public void ApplyRules_SetHeaderValueMutatesMessageAndStopRuleProcessingHaltsLaterRules()
    {
        var request = CreateRequest("From: Sender <sender@example.test>\r\nSubject: Test\r\n\r\nBody\r\n");
        var firstRule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 20,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.From,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "sender@example.test"),
            actions:
            [
                CreateHeaderAction(21, "X-hMailServer-Rule", "matched"),
                new SmtpRuleAction(
                    Id: 22,
                    Type: SmtpRuleActionType.StopRuleProcessing,
                    SortOrder: 2,
                    ImapFolder: string.Empty,
                    Subject: string.Empty,
                    FromName: string.Empty,
                    FromAddress: string.Empty,
                    To: string.Empty,
                    Body: string.Empty,
                    FileName: string.Empty,
                    ScriptFunction: string.Empty,
                    HeaderName: string.Empty,
                    Value: string.Empty,
                    RouteId: 0,
                    AbortSpamFlagged: false)
            ]);
        var secondRule = CreateRule(
            id: 3,
            criteria: new SmtpRuleCriterion(
                Id: 30,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Test"),
            actions: CreateHeaderAction(31, "X-Should-Not-Run", "true"));

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { firstRule, secondRule });

        Assert.IsTrue(result.Accepted);
        Assert.IsFalse(result.DropMessage);
        using var stream = new MemoryStream(result.MessageData);
        var message = MimeMessage.Load(stream);
        Assert.AreEqual("matched", message.Headers["X-hMailServer-Rule"]);
        Assert.IsNull(message.Headers["X-Should-Not-Run"]);
    }

    [TestMethod]
    public void ApplyRules_MatchesRecipientListAndNumericSize()
    {
        var request = CreateRequest(
            "Subject: Size\r\n\r\nBody\r\n",
            recipients: [new SmtpResolvedRecipient("person@example.test", "person@example.test", 0, IsLocal: false)]);
        var rule = new SmtpRuleDefinition(
            Id: 4,
            Name: "recipient and size",
            UseAnd: true,
            SortOrder: 1,
            Criteria:
            [
                new SmtpRuleCriterion(40, true, SmtpRuleCriteriaField.RecipientList, string.Empty, SmtpRuleMatchType.Contains, "person@example.test"),
                new SmtpRuleCriterion(41, true, SmtpRuleCriteriaField.MessageSize, string.Empty, SmtpRuleMatchType.GreaterThan, "5")
            ],
            Actions: [CreateHeaderAction(42, "X-Size-Rule", "yes")]);

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        using var stream = new MemoryStream(result.MessageData);
        var message = MimeMessage.Load(stream);
        Assert.AreEqual("yes", message.Headers["X-Size-Rule"]);
    }

    [TestMethod]
    public void ApplyRules_ReturnsMoveToImapFolderAction()
    {
        var request = CreateRequest("Subject: Folder\r\n\r\nBody\r\n");
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 50,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Folder"),
            actions: new SmtpRuleAction(
                Id: 51,
                Type: SmtpRuleActionType.MoveToImapFolder,
                SortOrder: 1,
                ImapFolder: "Archive.2026",
                Subject: string.Empty,
                FromName: string.Empty,
                FromAddress: string.Empty,
                To: string.Empty,
                Body: string.Empty,
                FileName: string.Empty,
                ScriptFunction: string.Empty,
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false));

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.AreEqual("Archive.2026", result.MoveToImapFolder);
    }

    [TestMethod]
    public void ApplyRules_ForwardCreatesGeneratedQueueMessageAndIncrementsLoopCount()
    {
        var request = CreateRequest("Subject: Forward\r\n\r\nBody\r\n");
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 60,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Forward"),
            actions: new SmtpRuleAction(
                Id: 61,
                Type: SmtpRuleActionType.Forward,
                SortOrder: 1,
                ImapFolder: string.Empty,
                Subject: string.Empty,
                FromName: string.Empty,
                FromAddress: string.Empty,
                To: "Forward One <one@example.test>; two@example.test",
                Body: string.Empty,
                FileName: string.Empty,
                ScriptFunction: string.Empty,
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false));

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.AreEqual(1, result.GeneratedMessages.Count);
        CollectionAssert.AreEquivalent(
            new[] { "one@example.test", "two@example.test" },
            result.GeneratedMessages[0].Recipients.Select(static recipient => recipient.Address).ToArray());
        using var stream = new MemoryStream(result.GeneratedMessages[0].MessageData);
        var message = MimeMessage.Load(stream);
        Assert.AreEqual("1", message.Headers["X-hMailServer-LoopCount"]);
    }

    [TestMethod]
    [DataRow(false, false, 1)]
    [DataRow(false, true, 1)]
    [DataRow(true, false, 1)]
    [DataRow(true, true, 0)]
    public void ApplyRules_ForwardHonorsAbortSpamFlaggedOnlyForOriginalSpam(
        bool originalMessageSpamFlagged,
        bool abortSpamFlagged,
        int expectedGeneratedMessageCount)
    {
        var request = CreateRequest(
            "Subject: Forward\r\n\r\nBody\r\n",
            originalMessageSpamFlagged: originalMessageSpamFlagged);
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 62,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Forward"),
            actions:
            [
                CreateForwardAction(63, abortSpamFlagged),
                CreateHeaderAction(64, "X-After-Forward", "continued")
            ]);

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.AreEqual(expectedGeneratedMessageCount, result.GeneratedMessages.Count);
        using var stream = new MemoryStream(result.MessageData);
        var message = MimeMessage.Load(stream);
        Assert.AreEqual("continued", message.Headers["X-After-Forward"]);
    }

    [TestMethod]
    public void ApplyRules_ReturnsForcedRouteAndBindAddressActions()
    {
        var request = CreateRequest("Subject: Route\r\n\r\nBody\r\n");
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 65,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Route"),
            actions:
            [
                new SmtpRuleAction(
                    Id: 66,
                    Type: SmtpRuleActionType.SendUsingRoute,
                    SortOrder: 1,
                    ImapFolder: string.Empty,
                    Subject: string.Empty,
                    FromName: string.Empty,
                    FromAddress: string.Empty,
                    To: string.Empty,
                    Body: string.Empty,
                    FileName: string.Empty,
                    ScriptFunction: string.Empty,
                    HeaderName: string.Empty,
                    Value: string.Empty,
                    RouteId: 42,
                    AbortSpamFlagged: false),
                new SmtpRuleAction(
                    Id: 67,
                    Type: SmtpRuleActionType.BindToAddress,
                    SortOrder: 2,
                    ImapFolder: string.Empty,
                    Subject: string.Empty,
                    FromName: string.Empty,
                    FromAddress: string.Empty,
                    To: string.Empty,
                    Body: string.Empty,
                    FileName: string.Empty,
                    ScriptFunction: string.Empty,
                    HeaderName: string.Empty,
                    Value: " 192.0.2.25 ",
                    RouteId: 0,
                    AbortSpamFlagged: false)
            ]);

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.AreEqual(42, result.ForcedRouteId);
        Assert.AreEqual("192.0.2.25", result.BindToAddress);
    }

    [TestMethod]
    public void ApplyRules_ReplyCreatesAutoSubmittedGeneratedMessage()
    {
        var request = CreateRequest("Subject: Needs reply\r\n\r\nOriginal body\r\n");
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 68,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "reply"),
            actions: new SmtpRuleAction(
                Id: 69,
                Type: SmtpRuleActionType.Reply,
                SortOrder: 1,
                ImapFolder: string.Empty,
                Subject: "Auto reply",
                FromName: "Support",
                FromAddress: "support@example.test",
                To: string.Empty,
                Body: "Thanks for the message.",
                FileName: string.Empty,
                ScriptFunction: string.Empty,
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false));

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.AreEqual(1, result.GeneratedMessages.Count);
        var generated = result.GeneratedMessages[0];
        Assert.AreEqual("support@example.test", generated.MailFrom);
        Assert.AreEqual("sender@example.test", generated.Recipients.Single().Address);
        using var stream = new MemoryStream(generated.MessageData);
        var message = MimeMessage.Load(stream);
        Assert.AreEqual("support@example.test", message.From.Mailboxes.Single().Address);
        Assert.AreEqual("sender@example.test", message.To.Mailboxes.Single().Address);
        Assert.AreEqual("Auto reply", message.Subject);
        Assert.AreEqual("Thanks for the message.", (message.TextBody ?? string.Empty).TrimEnd('\r', '\n'));
        Assert.AreEqual("auto-replied", message.Headers["Auto-Submitted"]);
        Assert.AreEqual("1", message.Headers["X-hMailServer-LoopCount"]);
    }

    [TestMethod]
    public void ApplyRules_ReplySkipsAutoSubmittedMessages()
    {
        var request = CreateRequest("Subject: Needs reply\r\nAuto-Submitted: auto-generated\r\n\r\nOriginal body\r\n");
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 72,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "reply"),
            actions: new SmtpRuleAction(
                Id: 73,
                Type: SmtpRuleActionType.Reply,
                SortOrder: 1,
                ImapFolder: string.Empty,
                Subject: "Auto reply",
                FromName: "Support",
                FromAddress: "support@example.test",
                To: string.Empty,
                Body: "Thanks for the message.",
                FileName: string.Empty,
                ScriptFunction: string.Empty,
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false));

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.AreEqual(0, result.GeneratedMessages.Count);
    }

    [TestMethod]
    [DataRow(false, false, 1)]
    [DataRow(false, true, 1)]
    [DataRow(true, false, 1)]
    [DataRow(true, true, 0)]
    public void ApplyRules_ReplyHonorsAbortSpamFlaggedOnlyForOriginalSpam(
        bool originalMessageSpamFlagged,
        bool abortSpamFlagged,
        int expectedGeneratedMessageCount)
    {
        var request = CreateRequest(
            "From: Sender <sender@example.test>\r\nSubject: Needs reply\r\n\r\nOriginal body\r\n",
            originalMessageSpamFlagged: originalMessageSpamFlagged);
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 682,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "reply"),
            actions:
            [
                CreateReplyAction(683, abortSpamFlagged),
                CreateHeaderAction(684, "X-After-Reply", "continued")
            ]);

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.AreEqual(expectedGeneratedMessageCount, result.GeneratedMessages.Count);
        using var stream = new MemoryStream(result.MessageData);
        var message = MimeMessage.Load(stream);
        Assert.AreEqual("continued", message.Headers["X-After-Reply"]);
    }

    [TestMethod]
    public void ApplyRules_ScriptFunctionExecutorCanMutateMessage()
    {
        var request = CreateRequest("Subject: Script\r\n\r\nBody\r\n");
        SmtpRuleScriptExecutionRequest? capturedRequest = null;
        var executor = new FakeScriptExecutor(scriptRequest =>
        {
            capturedRequest = scriptRequest;
            using var input = new MemoryStream(scriptRequest.MessageData);
            var message = MimeMessage.Load(input);
            message.Headers.Add("X-Script-Function", scriptRequest.FunctionName);
            using var output = new MemoryStream();
            message.WriteTo(output);
            return SmtpRuleScriptExecutionResult.Continue(output.ToArray());
        });
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 74,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Script"),
            actions: new SmtpRuleAction(
                Id: 75,
                Type: SmtpRuleActionType.ScriptFunction,
                SortOrder: 1,
                ImapFolder: string.Empty,
                Subject: string.Empty,
                FromName: string.Empty,
                FromAddress: string.Empty,
                To: string.Empty,
                Body: string.Empty,
                FileName: string.Empty,
                ScriptFunction: "Rule_Custom",
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false));

        var result = SqlServerSmtpRuleProcessor.ApplyRules(
            request,
            new[] { rule },
            scriptExecutor: executor);

        Assert.IsTrue(result.Accepted);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("Rule_Custom", capturedRequest.FunctionName);
        Assert.AreEqual(1, capturedRequest.RuleId);
        Assert.AreEqual("sender@example.test", capturedRequest.MailFrom);
        using var stream = new MemoryStream(result.MessageData);
        var message = MimeMessage.Load(stream);
        Assert.AreEqual("Rule_Custom", message.Headers["X-Script-Function"]);
    }

    [TestMethod]
    public void ApplyRules_ScriptFunctionFailureRejectsMessage()
    {
        var request = CreateRequest("Subject: Script\r\n\r\nBody\r\n");
        var executor = new FakeScriptExecutor(_ =>
            SmtpRuleScriptExecutionResult.Failure("550 blocked by script"));
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 76,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Script"),
            actions: new SmtpRuleAction(
                Id: 77,
                Type: SmtpRuleActionType.ScriptFunction,
                SortOrder: 1,
                ImapFolder: string.Empty,
                Subject: string.Empty,
                FromName: string.Empty,
                FromAddress: string.Empty,
                To: string.Empty,
                Body: string.Empty,
                FileName: string.Empty,
                ScriptFunction: "Rule_Block",
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false));

        var result = SqlServerSmtpRuleProcessor.ApplyRules(
            request,
            new[] { rule },
            scriptExecutor: executor);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("550 blocked by script", result.FailureResponse);
    }

    [TestMethod]
    public void ApplyRules_AccountScriptPreservesMessageCopyOperations()
    {
        var request = CreateRequest("Subject: Script copy\r\n\r\nBody\r\n");
        var copyData = Encoding.ASCII.GetBytes("Subject: Copied snapshot\r\n\r\nBody\r\n");
        var executor = new FakeScriptExecutor(_ =>
            SmtpRuleScriptExecutionResult
                .Continue(request.MessageData)
                .WithMessageCopyOperations([new ScriptMessageCopyOperation(55, copyData)]));
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 78,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Script copy"),
            actions: new SmtpRuleAction(
                Id: 79,
                Type: SmtpRuleActionType.ScriptFunction,
                SortOrder: 1,
                ImapFolder: string.Empty,
                Subject: string.Empty,
                FromName: string.Empty,
                FromAddress: string.Empty,
                To: string.Empty,
                Body: string.Empty,
                FileName: string.Empty,
                ScriptFunction: "Rule_Copy",
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false));

        var accountResult = SqlServerSmtpRuleProcessor.ApplyRules(
            request,
            [rule],
            scriptExecutor: executor,
            accountId: 123);
        var globalResult = SqlServerSmtpRuleProcessor.ApplyRules(
            request,
            [rule],
            scriptExecutor: executor,
            accountId: 0);

        Assert.AreEqual(1, accountResult.MessageCopyOperations?.Count);
        var copyOperation = accountResult.MessageCopyOperations![0];
        Assert.AreEqual(55, copyOperation.DestinationFolderId);
        CollectionAssert.AreEqual(copyData, copyOperation.MessageData);
        Assert.AreEqual(0, globalResult.MessageCopyOperations?.Count);
    }

    [TestMethod]
    public void ApplyRules_CreateCopyCopiesCurrentRecipientsAndSetsCopyRuleHeader()
    {
        var request = CreateRequest(
            "Subject: Copy\r\n\r\nBody\r\n",
            recipients: [new SmtpResolvedRecipient("copy@example.test", "copy@example.test", 0, IsLocal: false)]);
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 70,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Copy"),
            actions: new SmtpRuleAction(
                Id: 71,
                Type: SmtpRuleActionType.CreateCopy,
                SortOrder: 1,
                ImapFolder: string.Empty,
                Subject: string.Empty,
                FromName: string.Empty,
                FromAddress: string.Empty,
                To: string.Empty,
                Body: string.Empty,
                FileName: string.Empty,
                ScriptFunction: string.Empty,
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false));

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.AreEqual(1, result.GeneratedMessages.Count);
        Assert.AreEqual("copy@example.test", result.GeneratedMessages[0].Recipients.Single().Address);
        using var stream = new MemoryStream(result.GeneratedMessages[0].MessageData);
        var message = MimeMessage.Load(stream);
        Assert.AreEqual("rule", message.Headers["X-CopyRule"]);
        Assert.AreEqual("1", message.Headers["X-hMailServer-LoopCount"]);
    }

    [TestMethod]
    public void ApplyRules_DoesNotGenerateForwardWhenRuleLoopLimitIsReached()
    {
        var request = CreateRequest("Subject: Forward\r\nX-hMailServer-LoopCount: 5\r\n\r\nBody\r\n");
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 80,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Forward"),
            actions: new SmtpRuleAction(
                Id: 81,
                Type: SmtpRuleActionType.Forward,
                SortOrder: 1,
                ImapFolder: string.Empty,
                Subject: string.Empty,
                FromName: string.Empty,
                FromAddress: string.Empty,
                To: "one@example.test",
                Body: string.Empty,
                FileName: string.Empty,
                ScriptFunction: string.Empty,
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false));

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.AreEqual(0, result.GeneratedMessages.Count);
    }

    [TestMethod]
    public void ApplyRules_DeleteKeepsGeneratedForwardMessages()
    {
        var request = CreateRequest("Subject: Forward and drop\r\n\r\nBody\r\n");
        var rule = CreateRule(
            criteria: new SmtpRuleCriterion(
                Id: 90,
                UsePredefinedField: true,
                PredefinedField: SmtpRuleCriteriaField.Subject,
                HeaderName: string.Empty,
                MatchType: SmtpRuleMatchType.Contains,
                MatchValue: "Forward"),
            actions:
            [
                new SmtpRuleAction(
                    Id: 91,
                    Type: SmtpRuleActionType.Forward,
                    SortOrder: 1,
                    ImapFolder: string.Empty,
                    Subject: string.Empty,
                    FromName: string.Empty,
                    FromAddress: string.Empty,
                    To: "archive@example.test",
                    Body: string.Empty,
                    FileName: string.Empty,
                    ScriptFunction: string.Empty,
                    HeaderName: string.Empty,
                    Value: string.Empty,
                    RouteId: 0,
                    AbortSpamFlagged: false),
                new SmtpRuleAction(
                    Id: 92,
                    Type: SmtpRuleActionType.Delete,
                    SortOrder: 2,
                    ImapFolder: string.Empty,
                    Subject: string.Empty,
                    FromName: string.Empty,
                    FromAddress: string.Empty,
                    To: string.Empty,
                    Body: string.Empty,
                    FileName: string.Empty,
                    ScriptFunction: string.Empty,
                    HeaderName: string.Empty,
                    Value: string.Empty,
                    RouteId: 0,
                    AbortSpamFlagged: false)
            ]);

        var result = SqlServerSmtpRuleProcessor.ApplyRules(request, new[] { rule });

        Assert.IsTrue(result.DropMessage);
        Assert.AreEqual(1, result.GeneratedMessages.Count);
        Assert.AreEqual("archive@example.test", result.GeneratedMessages[0].Recipients.Single().Address);
    }

    [TestMethod]
    public void SelectRulesForAccountSql_LoadsActiveRulesWithCriteriaAndActions()
    {
        StringAssert.Contains(SqlServerSmtpRuleProcessor.SelectRulesForAccountSql, "FROM hm_rules");
        StringAssert.Contains(SqlServerSmtpRuleProcessor.SelectRulesForAccountSql, "ruleaccountid = @AccountId");
        StringAssert.Contains(SqlServerSmtpRuleProcessor.SelectRulesForAccountSql, "ruleactive <> 0");
        StringAssert.Contains(SqlServerSmtpRuleProcessor.SelectRulesForAccountSql, "hm_rule_criterias");
        StringAssert.Contains(SqlServerSmtpRuleProcessor.SelectRulesForAccountSql, "hm_rule_actions");
        StringAssert.Contains(SqlServerSmtpRuleProcessor.SelectRulesForAccountSql, "actionabortspamflagged");
    }

    private static SmtpReceiveRequest CreateRequest(
        string message,
        bool? originalMessageSpamFlagged = null,
        params SmtpResolvedRecipient[] recipients)
    {
        return new SmtpReceiveRequest(
            HeloHost: "client.example",
            IsExtendedSmtp: true,
            MailFrom: "sender@example.test",
            Recipients: recipients.Length == 0
                ? [new SmtpResolvedRecipient("recipient@example.test", "recipient@example.test", 0, IsLocal: false)]
                : recipients,
            DeclaredSize: null,
            MessageData: Encoding.Latin1.GetBytes(message),
            ReceivedUtc: DateTimeOffset.UtcNow,
            OriginalMessageSpamFlagged: originalMessageSpamFlagged);
    }

    private static SmtpRuleAction CreateForwardAction(
        long id,
        bool abortSpamFlagged) =>
        new(
            Id: id,
            Type: SmtpRuleActionType.Forward,
            SortOrder: (int)id,
            ImapFolder: string.Empty,
            Subject: string.Empty,
            FromName: string.Empty,
            FromAddress: string.Empty,
            To: "forward@example.test",
            Body: string.Empty,
            FileName: string.Empty,
            ScriptFunction: string.Empty,
            HeaderName: string.Empty,
            Value: string.Empty,
            RouteId: 0,
            AbortSpamFlagged: abortSpamFlagged);

    private static SmtpRuleAction CreateReplyAction(
        long id,
        bool abortSpamFlagged) =>
        new(
            Id: id,
            Type: SmtpRuleActionType.Reply,
            SortOrder: (int)id,
            ImapFolder: string.Empty,
            Subject: "Auto reply",
            FromName: "Support",
            FromAddress: "support@example.test",
            To: string.Empty,
            Body: "Thanks for the message.",
            FileName: string.Empty,
            ScriptFunction: string.Empty,
            HeaderName: string.Empty,
            Value: string.Empty,
            RouteId: 0,
            AbortSpamFlagged: abortSpamFlagged);

    private static SmtpRuleDefinition CreateRule(
        SmtpRuleCriterion criteria,
        params SmtpRuleAction[] actions) =>
        CreateRule(id: 1, criteria, actions);

    private static SmtpRuleDefinition CreateRule(
        long id,
        SmtpRuleCriterion criteria,
        params SmtpRuleAction[] actions) =>
        new(
            Id: id,
            Name: "rule",
            UseAnd: true,
            SortOrder: (int)id,
            Criteria: [criteria],
            Actions: actions);

    private static SmtpRuleAction CreateHeaderAction(
        long id,
        string headerName,
        string value) =>
        new(
            Id: id,
            Type: SmtpRuleActionType.SetHeaderValue,
            SortOrder: (int)id,
            ImapFolder: string.Empty,
            Subject: string.Empty,
            FromName: string.Empty,
            FromAddress: string.Empty,
            To: string.Empty,
            Body: string.Empty,
            FileName: string.Empty,
            ScriptFunction: string.Empty,
            HeaderName: headerName,
            Value: value,
            RouteId: 0,
            AbortSpamFlagged: false);

    private sealed class FakeScriptExecutor : ISmtpRuleScriptExecutor
    {
        private readonly Func<SmtpRuleScriptExecutionRequest, SmtpRuleScriptExecutionResult> _execute;

        public FakeScriptExecutor(Func<SmtpRuleScriptExecutionRequest, SmtpRuleScriptExecutionResult> execute)
        {
            _execute = execute;
        }

        public SmtpRuleScriptExecutionResult Execute(
            SmtpRuleScriptExecutionRequest request,
            CancellationToken cancellationToken) =>
            _execute(request);
    }
}
