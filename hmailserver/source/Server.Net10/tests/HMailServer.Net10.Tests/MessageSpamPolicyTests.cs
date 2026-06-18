using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class MessageSpamPolicyTests
{
    [TestMethod]
    public void Apply_MarksMessageWhenScoreMeetsThreshold()
    {
        var messageData = "Subject: Borderline\r\n\r\nBody\r\n"u8.ToArray();
        var policy = new MessageSpamPolicy(
            new MessageSpamPolicyOptions
            {
                SpamMarkThreshold = 5
            });

        var result = policy.Apply(
            messageData,
            MessageSpamScanResult.Clean(messageData, details: "Score threshold", score: 5));

        Assert.IsTrue(result.MarkAsSpam);
        CollectionAssert.AreEqual(messageData, result.MessageData);
    }

    [TestMethod]
    public void Apply_AddsLegacyHeadersForThresholdMarkedMessage()
    {
        var messageData = Encoding.Latin1.GetBytes("Subject: Original\r\n\r\nBody\r\n");
        var policy = new MessageSpamPolicy(
            new MessageSpamPolicyOptions
            {
                AddSpamHeader = true,
                AddReasonHeaders = true,
                PrependSubject = true,
                SpamMarkThreshold = 5,
                SubjectPrefix = "[SPAM]"
            });

        var result = policy.Apply(
            messageData,
            MessageSpamScanResult.Clean(messageData, details: "Score threshold", score: 6));

        Assert.IsTrue(result.MarkAsSpam);
        var text = Encoding.Latin1.GetString(result.MessageData);
        StringAssert.Contains(text, "Subject: [SPAM] Original\r\n");
        StringAssert.Contains(text, "X-hMailServer-Spam: YES\r\n");
        StringAssert.Contains(text, "X-hMailServer-Reason-1: Score threshold - (Score: 6)\r\n");
        StringAssert.Contains(text, "X-hMailServer-Reason-Score: 6\r\n");
    }

    [TestMethod]
    public void Apply_RejectsMessageWhenScoreMeetsDeleteThreshold()
    {
        var messageData = "Subject: Delete\r\n\r\nBody\r\n"u8.ToArray();
        var policy = new MessageSpamPolicy(
            new MessageSpamPolicyOptions
            {
                SpamMarkThreshold = 5,
                SpamDeleteThreshold = 10
            });

        var result = policy.Apply(
            messageData,
            MessageSpamScanResult.Clean(messageData, details: "Score delete threshold", score: 10));

        Assert.IsTrue(result.RejectMessage);
        Assert.IsFalse(result.MarkAsSpam);
        Assert.AreEqual("554 Score delete threshold", result.FailureResponse);
        CollectionAssert.AreEqual(messageData, result.MessageData);
    }
}
