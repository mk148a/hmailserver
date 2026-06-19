using System.Text;
using HMailServer.Core.Abstractions;
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

    [TestMethod]
    public void BuildBounceMessage_RendersDefaultTemplateWithQueueMetadata()
    {
        var message = CreateMessage();
        var recipient = new DeliveryQueueRecipient(
            7,
            "user@remote.test",
            "alias@remote.test",
            LocalAccountId: 0);

        var bytes = SqlServerDeliveryBounceStore.BuildBounceMessage(
            DeliveryBounceOptions.Default("mx.example.test"),
            message,
            [recipient],
            "550 No such user.",
            DateTimeOffset.Parse("2026-02-03T04:05:06Z", System.Globalization.CultureInfo.InvariantCulture));
        var text = Encoding.UTF8.GetString(bytes);

        StringAssert.Contains(text, "From: MAILER-DAEMON@mx.example.test\r\n");
        StringAssert.Contains(text, "To: sender@example.test\r\n");
        StringAssert.Contains(text, "Subject: Undeliverable: message 51\r\n");
        StringAssert.Contains(text, "X-hMailServer-Queue-Message-Id: 51\r\n");
        StringAssert.Contains(text, "X-hMailServer-Delivery-Attempt: 2\r\n");
        StringAssert.Contains(text, "Server: mx.example.test");
        StringAssert.Contains(text, "Original message UID: 99");
        StringAssert.Contains(text, "Original file: queue.eml");
        StringAssert.Contains(text, "Original size: 2048");
        StringAssert.Contains(text, "Original state: 32");
        StringAssert.Contains(text, "Delivery attempt: 3");
        StringAssert.Contains(text, "Retry count: 2");
        StringAssert.Contains(text, "Failed recipient count: 1");
        StringAssert.Contains(text, " - user@remote.test (original: alias@remote.test)");
        StringAssert.Contains(text, "550 No such user.");
    }

    [TestMethod]
    public void BuildBounceMessage_AppliesCustomTemplatesAndSanitizesSubjectHeader()
    {
        var options = DeliveryBounceOptions.Default("mx.example.test") with
        {
            SubjectTemplate = "Custom {MessageId}/{MessageUid}\r\nInjected: no",
            BodyTemplate = "Attempt={DeliveryAttempt}\nState={MessageState}\nFailed={FailedRecipientCount}:{FailedRecipientAddresses}:{FirstFailedRecipient}\nRoute={RuleForcedRouteId}/{RuleBindAddress}\nReason={FailureDescription}",
            MaxFailureDescriptionLength = 8
        };

        var bytes = SqlServerDeliveryBounceStore.BuildBounceMessage(
            options,
            CreateMessage() with
            {
                RuleForcedRouteId = 77,
                RuleBindAddress = "192.0.2.10"
            },
            [new DeliveryQueueRecipient(8, "user@remote.test", "user@remote.test", LocalAccountId: 0)],
            "1234567890",
            DateTimeOffset.Parse("2026-02-03T04:05:06Z", System.Globalization.CultureInfo.InvariantCulture));
        var text = Encoding.UTF8.GetString(bytes);

        StringAssert.Contains(text, "Subject: Custom 51/99 Injected: no\r\n");
        StringAssert.Contains(text, "Attempt=3\r\n");
        StringAssert.Contains(text, "State=32\r\n");
        StringAssert.Contains(text, "Failed=1:user@remote.test:user@remote.test\r\n");
        StringAssert.Contains(text, "Route=77/192.0.2.10\r\n");
        StringAssert.Contains(text, "Reason=12345678\r\n");
        Assert.IsFalse(text.Contains("Reason=123456789", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildBounceMessage_SanitizesAddressHeaders()
    {
        var options = DeliveryBounceOptions.Default("mx.example.test") with
        {
            MailerDaemonAddress = "MAILER-DAEMON@mx.example.test\r\nInjected-From: no"
        };
        var message = CreateMessage() with
        {
            FromAddress = "sender@example.test\r\nInjected-To: no"
        };

        var bytes = SqlServerDeliveryBounceStore.BuildBounceMessage(
            options,
            message,
            [new DeliveryQueueRecipient(8, "user@remote.test", "user@remote.test", LocalAccountId: 0)],
            "550 No such user.",
            DateTimeOffset.Parse("2026-02-03T04:05:06Z", System.Globalization.CultureInfo.InvariantCulture));
        var text = Encoding.UTF8.GetString(bytes);
        var headerText = text[..text.IndexOf("\r\n\r\n", StringComparison.Ordinal)];

        StringAssert.Contains(text, "From: MAILER-DAEMON@mx.example.test Injected-From: no\r\n");
        StringAssert.Contains(text, "To: sender@example.test Injected-To: no\r\n");
        Assert.IsFalse(headerText.Contains("\r\nInjected-From:", StringComparison.Ordinal));
        Assert.IsFalse(headerText.Contains("\r\nInjected-To:", StringComparison.Ordinal));
    }

    private static DeliveryQueuedMessage CreateMessage() =>
        new(
            new MessageIdentity(51, 12, 34, 99),
            "queue.eml",
            "sender@example.test",
            Size: 2048,
            CreatedUtc: DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture),
            Flags: ImapMessageFlags.Recent,
            CurrentRetryCount: 2,
            Recipients: []);
}
