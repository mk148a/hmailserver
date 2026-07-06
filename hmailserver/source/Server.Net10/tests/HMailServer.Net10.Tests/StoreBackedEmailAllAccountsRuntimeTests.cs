using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using MimeKit;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class StoreBackedEmailAllAccountsRuntimeTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 6, 12, 34, 56, TimeSpan.Zero);

    [TestMethod]
    public async Task EmailAllAccounts_FiltersLegacyWildcardAndQueuesOneLocalMessage()
    {
        var store = new FixedRecipientStore(
            new EmailAllAccountsRecipient(1, "alice@example.test"),
            new EmailAllAccountsRecipient(2, "bob@example.test"),
            new EmailAllAccountsRecipient(3, "ALICIA@example.test"));
        var writer = new RecordingQueueWriter();
        var runtime = new StoreBackedEmailAllAccountsRuntime(
            store,
            writer,
            new FixedTimeProvider(FixedNow));

        var result = await runtime.EmailAllAccountsAsync(
            "AL?CE@*.TEST",
            "admin@example.test",
            "Server Admin",
            "Maintenance",
            "Planned work",
            CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(1, writer.Requests.Count);
        var request = writer.Requests[0];
        Assert.AreEqual(string.Empty, request.MailFrom);
        Assert.AreEqual(FixedNow, request.ReceivedUtc);
        Assert.AreEqual(1, request.Recipients.Count);
        var recipient = request.Recipients[0];
        Assert.AreEqual("alice@example.test", recipient.Address);
        Assert.AreEqual(string.Empty, recipient.OriginalAddress);
        Assert.AreEqual(1, recipient.LocalAccountId);
        Assert.IsTrue(recipient.IsLocal);
        Assert.IsFalse(recipient.IsRouteRecipient);

        using var stream = new MemoryStream(request.MessageData, writable: false);
        var message = await MimeMessage.LoadAsync(stream);
        var from = message.From.Mailboxes.Single();
        Assert.AreEqual("Server Admin", from.Name);
        Assert.AreEqual("admin@example.test", from.Address);
        Assert.AreEqual("Maintenance", message.Subject);
        Assert.AreEqual("Planned work\r\n", message.TextBody);
        Assert.AreEqual(0, message.To.Count);
        Assert.IsFalse(string.IsNullOrWhiteSpace(message.MessageId));
        Assert.AreEqual(FixedNow, message.Date);
    }

    [TestMethod]
    public async Task EmailAllAccounts_QueuesLegacyMessageWhenWildcardMatchesNoAccounts()
    {
        var writer = new RecordingQueueWriter();
        var runtime = new StoreBackedEmailAllAccountsRuntime(
            new FixedRecipientStore(new EmailAllAccountsRecipient(1, "alice@example.test")),
            writer,
            new FixedTimeProvider(FixedNow));

        var result = await runtime.EmailAllAccountsAsync(
            "nobody@*",
            "admin@example.test",
            "Admin",
            "Subject",
            "Body",
            CancellationToken.None);

        Assert.IsTrue(result);
        Assert.AreEqual(1, writer.Requests.Count);
        Assert.AreEqual(0, writer.Requests[0].Recipients.Count);
        Assert.IsTrue(writer.Requests[0].AllowEmptyRecipients);
    }

    [TestMethod]
    public async Task EmailAllAccounts_ReturnsFalseWhenRecipientReadOrQueueWriteFails()
    {
        var storeFailure = new StoreBackedEmailAllAccountsRuntime(
            new ThrowingRecipientStore(),
            new RecordingQueueWriter(),
            new FixedTimeProvider(FixedNow));
        var writerFailure = new StoreBackedEmailAllAccountsRuntime(
            new FixedRecipientStore(new EmailAllAccountsRecipient(1, "alice@example.test")),
            new RecordingQueueWriter { Exception = new IOException("write failed") },
            new FixedTimeProvider(FixedNow));

        Assert.IsFalse(
            await storeFailure.EmailAllAccountsAsync(
                "*", "from@example.test", "From", "Subject", "Body", CancellationToken.None));
        Assert.IsFalse(
            await writerFailure.EmailAllAccountsAsync(
                "*", "from@example.test", "From", "Subject", "Body", CancellationToken.None));
    }

    private sealed class FixedRecipientStore(params EmailAllAccountsRecipient[] recipients)
        : IEmailAllAccountsRecipientStore
    {
        public ValueTask<IReadOnlyList<EmailAllAccountsRecipient>> GetActiveRecipientsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<EmailAllAccountsRecipient>>(recipients);
    }

    private sealed class ThrowingRecipientStore : IEmailAllAccountsRecipientStore
    {
        public ValueTask<IReadOnlyList<EmailAllAccountsRecipient>> GetActiveRecipientsAsync(
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("read failed");
    }

    private sealed class RecordingQueueWriter : ISmtpQueueWriter
    {
        public Exception? Exception { get; init; }
        public List<SmtpQueueWriteRequest> Requests { get; } = [];

        public ValueTask EnqueueAsync(
            SmtpQueueWriteRequest request,
            CancellationToken cancellationToken)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            Requests.Add(request);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
