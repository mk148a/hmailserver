using System.Runtime.CompilerServices;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Pop3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ExternalFetchProcessorTests
{
    [TestMethod]
    public async Task RunBatchAsync_DownloadsNewMessageRunsScriptAndQueuesWithKnownUid()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-1", Size: 64),
            ToAsciiBytes(
                "Received: from pop3.example.test by hmail; Thu, 02 Jan 2025 03:04:05 +0000\r\n" +
                "From: sender@example.net\r\n" +
                "To: user@example.test\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var script = new FakeExternalAccountDownloadScriptExecutor(
            request => ExternalAccountDownloadScriptExecutionResult.Continue(
                AddHeader(request.MessageData!, "X-Script", request.RemoteUid)));
        var processor = CreateProcessor(store, session, receiver, script);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsLeased);
        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(0, result.AccountsFailed);
        Assert.AreEqual(1, result.MessagesDownloaded);
        Assert.AreEqual(1, result.MessagesAccepted);
        Assert.AreEqual(1, result.KnownUidsAdded);
        Assert.AreEqual("uid-1", store.AddedUids.Single());
        Assert.AreEqual(1, receiver.Requests.Count);
        Assert.AreEqual("sender@example.net", receiver.Requests[0].MailFrom);
        Assert.AreEqual(
            DateTimeOffset.Parse("2025-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture),
            receiver.Requests[0].ReceivedUtc);
        Assert.AreEqual("user@example.test", receiver.Requests[0].Recipients.Single().Address);
        Assert.AreEqual(42, receiver.Requests[0].Recipients.Single().LocalAccountId);
        Assert.IsTrue(receiver.Requests[0].EnableSpamScan);
        Assert.IsTrue(receiver.Requests[0].EnableAntivirusScan);
        var receivedMessage = Encoding.ASCII.GetString(receiver.Requests[0].MessageData);
        StringAssert.StartsWith(receivedMessage, "X-Script: uid-1\r\nX-hMailServer-ExternalAccount: External POP3\r\n");
        Assert.AreEqual("uid-1", script.Requests.Single().RemoteUid);
        Assert.IsNotNull(script.Requests.Single().MessageData);
        StringAssert.StartsWith(
            Encoding.ASCII.GetString(script.Requests.Single().MessageData!),
            "X-hMailServer-ExternalAccount: External POP3\r\nReceived:");
    }

    [TestMethod]
    public async Task RunBatchAsync_IgnoresSenderAboveLegacyLengthLimit()
    {
        var invalidSender = $"{new string('a', 242)}@example.test";
        Assert.AreEqual(255, invalidSender.Length);
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-invalid-sender", Size: 64),
            ToAsciiBytes(
                $"From: {invalidSender}\r\n" +
                "To: user@example.test\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.MessagesAccepted);
        Assert.AreEqual(string.Empty, receiver.Requests.Single().MailFrom);
    }

    [TestMethod]
    public async Task RunBatchAsync_QueuesEmptyRemoteMessageWithExternalAccountHeader()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-empty", Size: 0),
            []);
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(0, result.AccountsFailed);
        Assert.AreEqual(1, result.MessagesDownloaded);
        Assert.AreEqual(1, result.MessagesAccepted);
        Assert.AreEqual(1, result.KnownUidsAdded);
        Assert.AreEqual("uid-empty", store.AddedUids.Single());
        CollectionAssert.AreEqual(
            "X-hMailServer-ExternalAccount: External POP3\r\n"u8.ToArray(),
            receiver.Requests.Single().MessageData);
    }

    [TestMethod]
    public async Task RunBatchAsync_DeletesNewMessageWhenScriptReturnsNegativeRetention()
    {
        var account = CreateAccount(daysToKeep: 7);
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-negative-new", Size: 64),
            "From: sender@example.net\r\nTo: user@example.test\r\nSubject: fetched\r\n\r\nBody\r\n"u8.ToArray());
        var receiver = new FakeSmtpMessageReceiver();
        var script = new FakeExternalAccountDownloadScriptExecutor(
            static request => ExternalAccountDownloadScriptExecutionResult.DeleteAfter(-2, request.MessageData));
        var processor = CreateProcessor(store, session, receiver, script);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(1, result.MessagesDownloaded);
        Assert.AreEqual(1, result.MessagesAccepted);
        Assert.AreEqual(1, result.RemoteMessagesDeleted);
        Assert.AreEqual(0, result.KnownUidsAdded);
        Assert.AreEqual("uid-negative-new", session.DeletedUids.Single());
        Assert.AreEqual(0, store.AddedUids.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_DeletesKnownUidWhenScriptRequestsImmediateDelete()
    {
        var account = CreateAccount(daysToKeep: 7);
        var store = new FakeExternalFetchAccountStore(account)
        {
            KnownUids =
            [
                new ExternalFetchKnownUid(88, "uid-known", DateTime.UtcNow.AddDays(-1))
            ]
        };
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(2, "uid-known", Size: 128),
            "Subject: old\r\n\r\nBody\r\n"u8.ToArray());
        var script = new FakeExternalAccountDownloadScriptExecutor(
            static request => ExternalAccountDownloadScriptExecutionResult.DeleteImmediately(request.MessageData));
        var processor = CreateProcessor(store, session, new FakeSmtpMessageReceiver(), script);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(0, result.MessagesDownloaded);
        Assert.AreEqual(0, result.MessagesAccepted);
        Assert.AreEqual(1, result.RemoteMessagesDeleted);
        Assert.AreEqual(1, result.KnownUidsDeleted);
        Assert.AreEqual("uid-known", session.DeletedUids.Single());
        Assert.AreEqual(88, store.DeletedUidIds.Single());
        Assert.IsNull(script.Requests.Single().MessageData);
    }

    [TestMethod]
    public async Task RunBatchAsync_DeletesKnownUidWhenScriptReturnsNegativeRetention()
    {
        var account = CreateAccount(daysToKeep: 7);
        var store = new FakeExternalFetchAccountStore(account)
        {
            KnownUids =
            [
                new ExternalFetchKnownUid(89, "uid-negative-known", DateTime.UtcNow)
            ]
        };
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(2, "uid-negative-known", Size: 128),
            "Subject: old\r\n\r\nBody\r\n"u8.ToArray());
        var script = new FakeExternalAccountDownloadScriptExecutor(
            static request => ExternalAccountDownloadScriptExecutionResult.DeleteAfter(-2, request.MessageData));
        var processor = CreateProcessor(store, session, new FakeSmtpMessageReceiver(), script);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(0, result.MessagesDownloaded);
        Assert.AreEqual(1, result.RemoteMessagesDeleted);
        Assert.AreEqual(1, result.KnownUidsDeleted);
        Assert.AreEqual("uid-negative-known", session.DeletedUids.Single());
        Assert.AreEqual(89, store.DeletedUidIds.Single());
    }

    [TestMethod]
    public async Task RunBatchAsync_DeletesKnownUidAfterElapsedRetentionDays()
    {
        var account = CreateAccount(daysToKeep: 1);
        var store = new FakeExternalFetchAccountStore(account)
        {
            KnownUids =
            [
                new ExternalFetchKnownUid(
                    90,
                    "uid-elapsed-retention",
                    DateTimeOffset.Parse("2026-01-09T00:01:00Z", System.Globalization.CultureInfo.InvariantCulture).UtcDateTime)
            ]
        };
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(3, "uid-elapsed-retention", Size: 128),
            "Subject: old\r\n\r\nBody\r\n"u8.ToArray());
        var processor = CreateProcessor(
            store,
            session,
            new FakeSmtpMessageReceiver(),
            timeProvider: new FixedTimeProvider(
                DateTimeOffset.Parse("2026-01-10T23:59:00Z", System.Globalization.CultureInfo.InvariantCulture)));

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(1, result.RemoteMessagesDeleted);
        Assert.AreEqual(1, result.KnownUidsDeleted);
        Assert.AreEqual("uid-elapsed-retention", session.DeletedUids.Single());
        Assert.AreEqual(90, store.DeletedUidIds.Single());
    }

    [TestMethod]
    public async Task RunBatchAsync_KeepsKnownUidAtExactRetentionBoundary()
    {
        var account = CreateAccount(daysToKeep: 1);
        var store = new FakeExternalFetchAccountStore(account)
        {
            KnownUids =
            [
                new ExternalFetchKnownUid(
                    91,
                    "uid-retention-boundary",
                    DateTimeOffset.Parse("2026-01-09T23:59:00Z", System.Globalization.CultureInfo.InvariantCulture).UtcDateTime)
            ]
        };
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(4, "uid-retention-boundary", Size: 128),
            "Subject: retained\r\n\r\nBody\r\n"u8.ToArray());
        var processor = CreateProcessor(
            store,
            session,
            new FakeSmtpMessageReceiver(),
            timeProvider: new FixedTimeProvider(
                DateTimeOffset.Parse("2026-01-10T23:59:00Z", System.Globalization.CultureInfo.InvariantCulture)));

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(0, result.RemoteMessagesDeleted);
        Assert.AreEqual(0, result.KnownUidsDeleted);
        Assert.AreEqual(0, session.DeletedUids.Count);
        Assert.AreEqual(0, store.DeletedUidIds.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_SkipsDuplicateKnownUidsInSameBatch()
    {
        var account = CreateAccount(daysToKeep: 7);
        var store = new FakeExternalFetchAccountStore(account)
        {
            KnownUids =
            [
                new ExternalFetchKnownUid(
                    88,
                    "uid-known",
                    DateTimeOffset.Parse("2025-12-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture).UtcDateTime)
            ]
        };
        var session = new FakeExternalFetchSession(
            (
                new ExternalFetchRemoteMessage(1, "uid-known", Size: 128),
                "Subject: known-one\r\n\r\nBody\r\n"u8.ToArray()
            ),
            (
                new ExternalFetchRemoteMessage(2, "uid-known", Size: 128),
                "Subject: known-two\r\n\r\nBody\r\n"u8.ToArray()
            ));
        var script = new FakeExternalAccountDownloadScriptExecutor(
            static request => ExternalAccountDownloadScriptExecutionResult.Continue(request.MessageData));
        var processor = CreateProcessor(store, session, new FakeSmtpMessageReceiver(), script);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(0, result.MessagesDownloaded);
        Assert.AreEqual(0, result.MessagesAccepted);
        Assert.AreEqual(1, result.RemoteMessagesDeleted);
        Assert.AreEqual(1, result.KnownUidsDeleted);
        Assert.AreEqual("uid-known", session.DeletedUids.Single());
        Assert.AreEqual(88, store.DeletedUidIds.Single());
        Assert.AreEqual(1, script.Requests.Count);
        Assert.IsNull(script.Requests.Single().MessageData);
    }

    [TestMethod]
    public async Task RunBatchAsync_ToleratesDuplicateKnownUidRows()
    {
        var account = CreateAccount(daysToKeep: 0);
        var store = new FakeExternalFetchAccountStore(account)
        {
            KnownUids =
            [
                new ExternalFetchKnownUid(
                    88,
                    "uid-known",
                    DateTimeOffset.Parse("2025-12-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture).UtcDateTime),
                new ExternalFetchKnownUid(
                    89,
                    "uid-known",
                    DateTimeOffset.Parse("2025-12-02T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture).UtcDateTime)
            ]
        };
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-known", Size: 128),
            "Subject: known\r\n\r\nBody\r\n"u8.ToArray());
        var processor = CreateProcessor(store, session, new FakeSmtpMessageReceiver());

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(0, result.AccountsFailed);
        Assert.AreEqual(0, result.MessagesDownloaded);
        Assert.AreEqual(0, result.MessagesAccepted);
        Assert.AreEqual(0, result.RemoteMessagesDeleted);
        Assert.AreEqual(0, result.KnownUidsDeleted);
        Assert.AreEqual(0, session.DownloadedSequences.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_RemovesKnownUidsWhenRemoteListingIsEmpty()
    {
        var account = CreateAccount(daysToKeep: 7);
        var store = new FakeExternalFetchAccountStore(account)
        {
            KnownUids =
            [
                new ExternalFetchKnownUid(
                    88,
                    "uid-missing-one",
                    DateTimeOffset.Parse("2025-12-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture).UtcDateTime),
                new ExternalFetchKnownUid(
                    89,
                    "uid-missing-two",
                    DateTimeOffset.Parse("2025-12-02T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture).UtcDateTime)
            ]
        };
        var session = new FakeExternalFetchSession();
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsLeased);
        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(0, result.AccountsFailed);
        Assert.AreEqual(0, result.MessagesDownloaded);
        Assert.AreEqual(0, result.MessagesAccepted);
        Assert.AreEqual(0, result.RemoteMessagesDeleted);
        Assert.AreEqual(0, result.KnownUidsAdded);
        Assert.AreEqual(2, result.KnownUidsDeleted);
        Assert.AreEqual(77, store.CompletedFetchAccountIds.Single());
        Assert.AreEqual(0, store.ReleasedFetchAccountIds.Count);
        CollectionAssert.AreEqual(new[] { 88, 89 }, store.DeletedUidIds);
        Assert.AreEqual(0, session.DownloadedSequences.Count);
        Assert.AreEqual(0, session.DeletedUids.Count);
        Assert.AreEqual(0, receiver.Requests.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_CompletesLeaseWhenReceiverRejects()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-1", Size: 64),
            "From: sender@example.net\r\nTo: user@example.test\r\nSubject: fetched\r\n\r\nBody\r\n"u8.ToArray());
        var receiver = new FakeSmtpMessageReceiver(SmtpReceiveResult.Failure("451 rejected"));
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsLeased);
        Assert.AreEqual(0, result.AccountsCompleted);
        Assert.AreEqual(1, result.AccountsFailed);
        Assert.AreEqual(77, store.CompletedFetchAccountIds.Single());
        Assert.AreEqual(0, store.ReleasedFetchAccountIds.Count);
        Assert.AreEqual(0, store.AddedUids.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_CompletesLeaseWhenSessionFactoryConnectionFails()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession();
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(
            store,
            session,
            receiver,
            connectionException: new IOException("External POP3 connection failed."));

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsLeased);
        Assert.AreEqual(0, result.AccountsCompleted);
        Assert.AreEqual(1, result.AccountsFailed);
        Assert.AreEqual(77, store.CompletedFetchAccountIds.Single());
        Assert.AreEqual(0, store.ReleasedFetchAccountIds.Count);
        Assert.AreEqual(0, receiver.Requests.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_CompletesLeaseWhenExternalFetchOperationTimesOut()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession();
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(
            store,
            session,
            receiver,
            connectionException: new TimeoutException("External POP3 operation timed out."));

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsLeased);
        Assert.AreEqual(0, result.AccountsCompleted);
        Assert.AreEqual(1, result.AccountsFailed);
        Assert.AreEqual(77, store.CompletedFetchAccountIds.Single());
        Assert.AreEqual(0, store.ReleasedFetchAccountIds.Count);
        Assert.AreEqual(0, receiver.Requests.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_CompletesLeaseWhenMessageDownloadFails()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-retr-failure", Size: 64),
            "Subject: unavailable\r\n\r\nBody\r\n"u8.ToArray())
        {
            DownloadException = new InvalidOperationException("External POP3 command failed: -ERR unavailable")
        };
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsLeased);
        Assert.AreEqual(0, result.AccountsCompleted);
        Assert.AreEqual(1, result.AccountsFailed);
        Assert.AreEqual(0, result.MessagesDownloaded);
        Assert.AreEqual(0, result.MessagesAccepted);
        Assert.AreEqual(77, store.CompletedFetchAccountIds.Single());
        Assert.AreEqual(0, store.ReleasedFetchAccountIds.Count);
        Assert.AreEqual(0, receiver.Requests.Count);
        Assert.AreEqual(0, store.AddedUids.Count);
        Assert.AreEqual(0, session.DeletedUids.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_CompletesLeaseWhenMessageBodyTerminatesEarly()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-retr-truncated", Size: 64),
            "Subject: truncated\r\n\r\nBody\r\n"u8.ToArray())
        {
            DownloadException = new IOException("External POP3 server closed the connection.")
        };
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsLeased);
        Assert.AreEqual(0, result.AccountsCompleted);
        Assert.AreEqual(1, result.AccountsFailed);
        Assert.AreEqual(0, result.MessagesDownloaded);
        Assert.AreEqual(0, result.MessagesAccepted);
        Assert.AreEqual(0, result.RemoteMessagesDeleted);
        Assert.AreEqual(0, result.KnownUidsAdded);
        Assert.AreEqual(0, result.KnownUidsDeleted);
        Assert.AreEqual(77, store.CompletedFetchAccountIds.Single());
        Assert.AreEqual(0, store.ReleasedFetchAccountIds.Count);
        Assert.AreEqual(0, store.AddedUids.Count);
        Assert.AreEqual(0, store.DeletedUidIds.Count);
        Assert.AreEqual(1, session.DownloadedSequences.Single());
        Assert.AreEqual(0, session.DeletedUids.Count);
        Assert.AreEqual(0, receiver.Requests.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_CompletesLeaseWhenMessageListingTerminatesEarly()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-partial-listing", Size: 64),
            "Subject: partial\r\n\r\nBody\r\n"u8.ToArray())
        {
            ListException = new IOException("External POP3 server closed the connection.")
        };
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsLeased);
        Assert.AreEqual(0, result.AccountsCompleted);
        Assert.AreEqual(1, result.AccountsFailed);
        Assert.AreEqual(0, result.MessagesDownloaded);
        Assert.AreEqual(0, result.MessagesAccepted);
        Assert.AreEqual(0, result.RemoteMessagesDeleted);
        Assert.AreEqual(0, result.KnownUidsAdded);
        Assert.AreEqual(0, result.KnownUidsDeleted);
        Assert.AreEqual(77, store.CompletedFetchAccountIds.Single());
        Assert.AreEqual(0, store.ReleasedFetchAccountIds.Count);
        Assert.AreEqual(0, store.AddedUids.Count);
        Assert.AreEqual(0, store.DeletedUidIds.Count);
        Assert.AreEqual(0, session.DownloadedSequences.Count);
        Assert.AreEqual(0, session.DeletedUids.Count);
        Assert.AreEqual(0, receiver.Requests.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_PreservesKnownUidWhenRemoteDeleteTransportFails()
    {
        var account = CreateAccount(daysToKeep: 7);
        var store = new FakeExternalFetchAccountStore(account)
        {
            KnownUids =
            [
                new ExternalFetchKnownUid(
                    88,
                    "uid-delete-failure",
                    DateTimeOffset.Parse("2025-12-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture).UtcDateTime)
            ]
        };
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-delete-failure", Size: 64),
            "Subject: known\r\n\r\nBody\r\n"u8.ToArray())
        {
            DeleteException = new IOException("External POP3 server closed the connection.")
        };
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsLeased);
        Assert.AreEqual(0, result.AccountsCompleted);
        Assert.AreEqual(1, result.AccountsFailed);
        Assert.AreEqual(0, result.RemoteMessagesDeleted);
        Assert.AreEqual(0, result.KnownUidsDeleted);
        Assert.AreEqual(77, store.CompletedFetchAccountIds.Single());
        Assert.AreEqual(0, store.ReleasedFetchAccountIds.Count);
        Assert.AreEqual("uid-delete-failure", session.DeletedUids.Single());
        Assert.AreEqual(0, store.DeletedUidIds.Count);
        Assert.AreEqual(0, session.DownloadedSequences.Count);
        Assert.AreEqual(0, receiver.Requests.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_TracksUidWhenReceiverPermanentlyRejects()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-spam", Size: 64),
            "From: sender@example.net\r\nTo: user@example.test\r\nSubject: fetched\r\n\r\nBody\r\n"u8.ToArray());
        var receiver = new FakeSmtpMessageReceiver(SmtpReceiveResult.Failure("554 Score delete threshold"));
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsLeased);
        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(0, result.AccountsFailed);
        Assert.AreEqual(1, result.MessagesDownloaded);
        Assert.AreEqual(0, result.MessagesAccepted);
        Assert.AreEqual(1, result.KnownUidsAdded);
        Assert.AreEqual("uid-spam", store.AddedUids.Single());
        Assert.AreEqual(0, session.DeletedUids.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_SkipsQueueAndTracksUidWhenAntivirusFindsVirus()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-virus", Size: 64),
            "From: sender@example.net\r\nTo: user@example.test\r\nSubject: infected\r\n\r\nBody\r\n"u8.ToArray());
        var receiver = new FakeSmtpMessageReceiver();
        var antivirus = new FakeAntivirusScanner(
            MessageAntivirusScanResult.Infected("Eicar-Test-Signature"));
        var processor = CreateProcessor(
            store,
            session,
            receiver,
            antivirusScanner: antivirus);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(1, result.MessagesDownloaded);
        Assert.AreEqual(0, result.MessagesAccepted);
        Assert.AreEqual(0, receiver.Requests.Count);
        Assert.AreEqual("uid-virus", store.AddedUids.Single());
        Assert.AreEqual(1, antivirus.ScannedMessages.Count);
    }

    [TestMethod]
    public async Task RunBatchAsync_AddsRecipientFromReceivedForHeader()
    {
        var account = CreateAccount(mimeRecipientHeaders: "X-RCPT-TO");
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-received", Size: 64),
            ToAsciiBytes(
                "Received: from mx.example by hmail for <alias@example.test>; Thu, 02 Jan 2025 03:04:05 +0000\r\n" +
                "From: sender@example.net\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var recipientValidator = new FakeSmtpRecipientValidator(
            request => SmtpRecipientValidationResult.Accept(
                new SmtpResolvedRecipient(
                    "user@example.test",
                    request.RecipientAddress,
                    LocalAccountId: 42,
                    IsLocal: true)));
        var processor = CreateProcessor(
            store,
            session,
            receiver,
            recipientValidator: recipientValidator);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.MessagesAccepted);
        Assert.AreEqual("alias@example.test", recipientValidator.Requests.Single().RecipientAddress);
        var recipient = receiver.Requests.Single().Recipients.Single();
        Assert.AreEqual("user@example.test", recipient.Address);
        Assert.AreEqual("alias@example.test", recipient.OriginalAddress);
        Assert.AreEqual(42, recipient.LocalAccountId);
    }

    [TestMethod]
    public async Task RunBatchAsync_ProcessesReceivedHeaderWhenConfiguredNamesAreWhitespace()
    {
        var account = CreateAccount(mimeRecipientHeaders: " ");
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-whitespace-recipient-headers", Size: 64),
            ToAsciiBytes(
                "Received: from mx.example by hmail for <alias@example.test>; Thu, 02 Jan 2025 03:04:05 +0000\r\n" +
                "From: sender@example.net\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var recipientValidator = new FakeSmtpRecipientValidator(
            request => SmtpRecipientValidationResult.Accept(
                new SmtpResolvedRecipient(
                    "user@example.test",
                    request.RecipientAddress,
                    LocalAccountId: 42,
                    IsLocal: true)));
        var processor = CreateProcessor(
            store,
            session,
            receiver,
            recipientValidator: recipientValidator);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.MessagesAccepted);
        Assert.AreEqual("alias@example.test", recipientValidator.Requests.Single().RecipientAddress);
        Assert.AreEqual("alias@example.test", receiver.Requests.Single().Recipients.Single().OriginalAddress);
    }

    [TestMethod]
    public async Task RunBatchAsync_RejectsMalformedRecipientFromReceivedHeader()
    {
        var account = CreateAccount(
            enableRouteRecipients: true,
            mimeRecipientHeaders: "X-RCPT-TO");
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-malformed-received-recipient", Size: 64),
            ToAsciiBytes(
                "Received: from mx.example by hmail for <bad@@example.test>; Thu, 02 Jan 2025 03:04:05 +0000\r\n" +
                "From: sender@example.net\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.MessagesAccepted);
        var recipient = receiver.Requests.Single().Recipients.Single();
        Assert.AreEqual("user@example.test", recipient.Address);
        Assert.AreEqual(42, recipient.LocalAccountId);
        Assert.IsTrue(recipient.IsLocal);
    }

    [TestMethod]
    public async Task RunBatchAsync_IgnoresUppercaseForTokenInReceivedHeader()
    {
        var account = CreateAccount(
            enableRouteRecipients: true,
            mimeRecipientHeaders: "X-RCPT-TO");
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-uppercase-received-for", Size: 64),
            ToAsciiBytes(
                "Received: from mx.example by hmail FOR <route@example.net>; Thu, 02 Jan 2025 03:04:05 +0000\r\n" +
                "From: sender@example.net\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.MessagesAccepted);
        var recipient = receiver.Requests.Single().Recipients.Single();
        Assert.AreEqual("user@example.test", recipient.Address);
        Assert.AreEqual(42, recipient.LocalAccountId);
        Assert.IsTrue(recipient.IsLocal);
    }

    [TestMethod]
    public async Task RunBatchAsync_UsesFirstDuplicateConfiguredMimeRecipientHeader()
    {
        var account = CreateAccount(mimeRecipientHeaders: "X-RCPT-TO");
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-duplicate-recipient-header", Size: 64),
            ToAsciiBytes(
                "From: sender@example.net\r\n" +
                "X-RCPT-TO: first@example.test\r\n" +
                "X-RCPT-TO: second@example.test\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var recipientValidator = new FakeSmtpRecipientValidator(
            request => SmtpRecipientValidationResult.Accept(
                new SmtpResolvedRecipient(
                    request.RecipientAddress,
                    request.RecipientAddress,
                    LocalAccountId: 42,
                    IsLocal: true)));
        var processor = CreateProcessor(
            store,
            session,
            receiver,
            recipientValidator: recipientValidator);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.MessagesAccepted);
        Assert.AreEqual("first@example.test", recipientValidator.Requests.Single().RecipientAddress);
        Assert.AreEqual("first@example.test", receiver.Requests.Single().Recipients.Single().Address);
    }

    [TestMethod]
    public async Task RunBatchAsync_PreservesValidRecipientBesideMalformedAddress()
    {
        var account = CreateAccount(mimeRecipientHeaders: "X-RCPT-TO");
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-mixed-validity-recipients", Size: 64),
            ToAsciiBytes(
                "From: sender@example.net\r\n" +
                "X-RCPT-TO: bad@@example.test, \"Valid, Recipient\" <valid@example.test>\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var recipientValidator = new FakeSmtpRecipientValidator(
            request => SmtpRecipientValidationResult.Accept(
                new SmtpResolvedRecipient(
                    request.RecipientAddress,
                    request.RecipientAddress,
                    LocalAccountId: 42,
                    IsLocal: true)));
        var processor = CreateProcessor(
            store,
            session,
            receiver,
            recipientValidator: recipientValidator);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.MessagesAccepted);
        Assert.AreEqual("valid@example.test", recipientValidator.Requests.Single().RecipientAddress);
        Assert.AreEqual("valid@example.test", receiver.Requests.Single().Recipients.Single().Address);
    }

    [TestMethod]
    public async Task RunBatchAsync_DeduplicatesAliasesResolvingToSameRecipient()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-recipient-aliases", Size: 64),
            ToAsciiBytes(
                "From: sender@example.net\r\n" +
                "To: alias-one@example.test, alias-two@example.test\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var recipientValidator = new FakeSmtpRecipientValidator(
            request => SmtpRecipientValidationResult.Accept(
                new SmtpResolvedRecipient(
                    "user@example.test",
                    request.RecipientAddress,
                    LocalAccountId: 42,
                    IsLocal: true)));
        var processor = CreateProcessor(
            store,
            session,
            receiver,
            recipientValidator: recipientValidator);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.MessagesAccepted);
        Assert.AreEqual(2, recipientValidator.Requests.Count);
        var recipient = receiver.Requests.Single().Recipients.Single();
        Assert.AreEqual("user@example.test", recipient.Address);
        Assert.AreEqual("alias-one@example.test", recipient.OriginalAddress);
    }

    [TestMethod]
    public async Task RunBatchAsync_FiltersExternalMimeRecipientsWhenRouteRecipientsDisabled()
    {
        var account = CreateAccount(enableRouteRecipients: false);
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-mixed", Size: 64),
            ToAsciiBytes(
                "From: sender@example.net\r\n" +
                "To: user@example.test, external@example.net\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var recipientValidator = new FakeSmtpRecipientValidator(
            request => request.RecipientAddress.EndsWith("@example.test", StringComparison.OrdinalIgnoreCase)
                ? SmtpRecipientValidationResult.Accept(
                    new SmtpResolvedRecipient(
                        request.RecipientAddress,
                        request.RecipientAddress,
                        LocalAccountId: 42,
                        IsLocal: true))
                : SmtpRecipientValidationResult.Accept(
                    new SmtpResolvedRecipient(
                        request.RecipientAddress,
                        request.RecipientAddress,
                        LocalAccountId: 0,
                        IsLocal: false)));
        var processor = CreateProcessor(
            store,
            session,
            receiver,
            recipientValidator: recipientValidator);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.MessagesAccepted);
        CollectionAssert.AreEquivalent(
            new[] { "user@example.test", "external@example.net" },
            recipientValidator.Requests.Select(static request => request.RecipientAddress).ToArray());
        Assert.AreEqual("user@example.test", receiver.Requests.Single().Recipients.Single().Address);
    }

    [TestMethod]
    public async Task RunBatchAsync_KeepsRouteLocalRecipientWhenRouteRecipientsEnabled()
    {
        var account = CreateAccount(enableRouteRecipients: true);
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            new ExternalFetchRemoteMessage(1, "uid-route", Size: 64),
            ToAsciiBytes(
                "From: sender@example.net\r\n" +
                "To: routed@example.net\r\n" +
                "Subject: fetched\r\n" +
                "\r\n" +
                "Body\r\n"));
        var receiver = new FakeSmtpMessageReceiver();
        var recipientValidator = new FakeSmtpRecipientValidator(
            request => SmtpRecipientValidationResult.Accept(
                new SmtpResolvedRecipient(
                    request.RecipientAddress,
                    request.RecipientAddress,
                    LocalAccountId: 0,
                    IsLocal: true,
                    Route: new SmtpRouteResolution(
                        RouteId: 5,
                        DomainName: "example.net",
                        TargetHost: "route.example.net",
                        TargetPort: 25,
                        ConnectionSecurity: 0,
                        TreatRecipientAsLocal: true,
                        RequiresAuthentication: false,
                        AuthenticationUsername: "",
                        AuthenticationPassword: ""))));
        var processor = CreateProcessor(
            store,
            session,
            receiver,
            recipientValidator: recipientValidator);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.MessagesAccepted);
        var recipient = receiver.Requests.Single().Recipients.Single();
        Assert.AreEqual("routed@example.net", recipient.Address);
        Assert.IsTrue(recipient.IsRouteRecipient);
        Assert.IsTrue(recipient.IsLocal);
    }

    [TestMethod]
    public async Task RunBatchAsync_SkipsDuplicateRemoteUidsInSameBatch()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            (
                new ExternalFetchRemoteMessage(1, "uid-duplicate", Size: 64),
                ToAsciiBytes("From: sender@example.net\r\nTo: user@example.test\r\nSubject: first\r\n\r\nBody\r\n")
            ),
            (
                new ExternalFetchRemoteMessage(2, "uid-duplicate", Size: 64),
                ToAsciiBytes("From: sender@example.net\r\nTo: user@example.test\r\nSubject: duplicate\r\n\r\nBody\r\n")
            ));
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(1, result.MessagesDownloaded);
        Assert.AreEqual(1, result.MessagesAccepted);
        Assert.AreEqual(1, result.KnownUidsAdded);
        Assert.AreEqual("uid-duplicate", store.AddedUids.Single());
        Assert.AreEqual(1, receiver.Requests.Count);
        CollectionAssert.AreEqual(new[] { 1 }, session.DownloadedSequences.ToArray());
    }

    [TestMethod]
    public async Task RunBatchAsync_SkipsDuplicateRemoteSequencesInSameBatch()
    {
        var account = CreateAccount();
        var store = new FakeExternalFetchAccountStore(account);
        var session = new FakeExternalFetchSession(
            [
                new ExternalFetchRemoteMessage(2, "uid-two", Size: 64),
                new ExternalFetchRemoteMessage(1, "uid-first", Size: 64),
                new ExternalFetchRemoteMessage(1, "uid-last", Size: 64)
            ],
            new Dictionary<int, byte[]>
            {
                [1] = ToAsciiBytes("From: sender@example.net\r\nTo: user@example.test\r\nSubject: duplicate sequence\r\n\r\nBody\r\n"),
                [2] = ToAsciiBytes("From: sender@example.net\r\nTo: user@example.test\r\nSubject: out of order\r\n\r\nBody\r\n")
            });
        var receiver = new FakeSmtpMessageReceiver();
        var processor = CreateProcessor(store, session, receiver);

        var result = await processor.RunBatchAsync(ExternalFetchProcessorOptions.Default, CancellationToken.None);

        Assert.AreEqual(1, result.AccountsCompleted);
        Assert.AreEqual(2, result.MessagesDownloaded);
        Assert.AreEqual(2, result.MessagesAccepted);
        Assert.AreEqual(2, result.KnownUidsAdded);
        CollectionAssert.AreEqual(new[] { "uid-last", "uid-two" }, store.AddedUids.ToArray());
        Assert.AreEqual(2, receiver.Requests.Count);
        CollectionAssert.AreEqual(new[] { 1, 2 }, session.DownloadedSequences.ToArray());
    }

    [TestMethod]
    public async Task ResetLocksAsync_ClearsStaleAccountLocks()
    {
        var store = new FakeExternalFetchAccountStore();
        var processor = CreateProcessor(
            store,
            new FakeExternalFetchSession(
                new ExternalFetchRemoteMessage(1, "uid-unused", Size: 64),
                "Subject: unused\r\n\r\nBody\r\n"u8.ToArray()),
            new FakeSmtpMessageReceiver());

        await processor.ResetLocksAsync(CancellationToken.None);

        Assert.AreEqual(1, store.ResetLocksCalls);
    }

    private static ExternalFetchProcessor CreateProcessor(
        FakeExternalFetchAccountStore store,
        FakeExternalFetchSession session,
        FakeSmtpMessageReceiver receiver,
        IExternalAccountDownloadScriptExecutor? scriptExecutor = null,
        IMessageAntivirusScanner? antivirusScanner = null,
        ISmtpRecipientValidator? recipientValidator = null,
        TimeProvider? timeProvider = null,
        Exception? connectionException = null) =>
        new(
            store,
            new FakeExternalFetchSessionFactory(session, connectionException),
            receiver,
            scriptExecutor,
            antivirusScanner,
            recipientValidator,
            timeProvider: timeProvider ?? new FixedTimeProvider(DateTimeOffset.Parse("2026-01-10T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)));

    private static ExternalFetchAccountLease CreateAccount(
        int daysToKeep = 7,
        bool enableRouteRecipients = false,
        string mimeRecipientHeaders = "To,CC,X-RCPT-TO,X-Envelope-To") =>
        new(
            FetchAccountId: 77,
            AccountId: 42,
            Name: "External POP3",
            ServerAddress: "pop3.example.test",
            ServerPort: 995,
            ServerType: ExternalFetchServerType.Pop3,
            Username: "external-user",
            Password: "external-password",
            MinutesBetweenFetch: 10,
            DaysToKeep: daysToKeep,
            ProcessMimeRecipients: true,
            ProcessMimeDate: true,
            ConnectionSecurity: ExternalFetchConnectionSecurity.Ssl,
            UseAntiSpam: true,
            UseAntiVirus: true,
            EnableRouteRecipients: enableRouteRecipients,
            MimeRecipientHeaders: mimeRecipientHeaders,
            AccountAddress: "user@example.test");

    private static byte[] AddHeader(byte[] messageData, string name, string value)
    {
        var message = Encoding.ASCII.GetString(messageData);
        return Encoding.ASCII.GetBytes($"{name}: {value}\r\n" + message);
    }

    private static byte[] ToAsciiBytes(string value) =>
        Encoding.ASCII.GetBytes(value);

    private sealed class FakeExternalFetchAccountStore : IExternalFetchAccountStore
    {
        private readonly IReadOnlyList<ExternalFetchAccountLease> _accounts;

        public FakeExternalFetchAccountStore(params ExternalFetchAccountLease[] accounts)
        {
            _accounts = accounts;
        }

        public IReadOnlyList<ExternalFetchKnownUid> KnownUids { get; init; } = [];

        public List<string> AddedUids { get; } = [];

        public List<int> DeletedUidIds { get; } = [];

        public List<int> CompletedFetchAccountIds { get; } = [];

        public List<int> ReleasedFetchAccountIds { get; } = [];

        public int ResetLocksCalls { get; private set; }

        public async IAsyncEnumerable<ExternalFetchAccountLease> LeaseReadyAccountsAsync(
            int batchSize,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var account in _accounts.Take(batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return account;
                await Task.Yield();
            }
        }

        public ValueTask<int> DeferInactiveAccountsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(0);

        public ValueTask<bool> CompleteAsync(
            int fetchAccountId,
            CancellationToken cancellationToken)
        {
            CompletedFetchAccountIds.Add(fetchAccountId);
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> ReleaseAsync(
            int fetchAccountId,
            CancellationToken cancellationToken)
        {
            ReleasedFetchAccountIds.Add(fetchAccountId);
            return ValueTask.FromResult(true);
        }

        public ValueTask ResetLocksAsync(CancellationToken cancellationToken)
        {
            ResetLocksCalls++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<ExternalFetchKnownUid>> LoadKnownUidsAsync(
            int fetchAccountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(KnownUids);

        public ValueTask AddKnownUidAsync(
            int fetchAccountId,
            string uid,
            CancellationToken cancellationToken)
        {
            AddedUids.Add(uid);
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteKnownUidAsync(
            int uidId,
            CancellationToken cancellationToken)
        {
            DeletedUidIds.Add(uidId);
            return ValueTask.FromResult(true);
        }
    }

    private sealed class FakeExternalFetchSessionFactory : IExternalFetchSessionFactory
    {
        private readonly FakeExternalFetchSession _session;
        private readonly Exception? _connectionException;

        public FakeExternalFetchSessionFactory(
            FakeExternalFetchSession session,
            Exception? connectionException = null)
        {
            _session = session;
            _connectionException = connectionException;
        }

        public ValueTask<IExternalFetchSession> ConnectAsync(
            ExternalFetchAccountLease account,
            CancellationToken cancellationToken) =>
            _connectionException is null
                ? ValueTask.FromResult<IExternalFetchSession>(_session)
                : ValueTask.FromException<IExternalFetchSession>(_connectionException);
    }

    private sealed class FakeExternalFetchSession : IExternalFetchSession
    {
        private readonly IReadOnlyDictionary<int, byte[]> _messageDataBySequence;
        private readonly IReadOnlyList<ExternalFetchRemoteMessage> _messages;

        public FakeExternalFetchSession(
            ExternalFetchRemoteMessage message,
            byte[] messageData)
        {
            _messages = [message];
            _messageDataBySequence = new Dictionary<int, byte[]>
            {
                [message.SequenceNumber] = messageData
            };
        }

        public FakeExternalFetchSession(params (ExternalFetchRemoteMessage Message, byte[] MessageData)[] messages)
        {
            _messages = messages
                .Select(static entry => entry.Message)
                .ToArray();
            _messageDataBySequence = messages.ToDictionary(
                static entry => entry.Message.SequenceNumber,
                static entry => entry.MessageData);
        }

        public FakeExternalFetchSession(
            IReadOnlyList<ExternalFetchRemoteMessage> messages,
            IReadOnlyDictionary<int, byte[]> messageDataBySequence)
        {
            _messages = messages;
            _messageDataBySequence = messageDataBySequence;
        }

        public List<string> DeletedUids { get; } = [];

        public List<int> DownloadedSequences { get; } = [];

        public Exception? ListException { get; init; }

        public Exception? DownloadException { get; init; }

        public Exception? DeleteException { get; init; }

        public ValueTask<IReadOnlyList<ExternalFetchRemoteMessage>> ListMessagesAsync(
            CancellationToken cancellationToken)
        {
            if (ListException is not null)
            {
                return ValueTask.FromException<IReadOnlyList<ExternalFetchRemoteMessage>>(ListException);
            }

            return ValueTask.FromResult(_messages);
        }

        public ValueTask<byte[]> DownloadMessageAsync(
            ExternalFetchRemoteMessage message,
            CancellationToken cancellationToken)
        {
            DownloadedSequences.Add(message.SequenceNumber);
            if (DownloadException is not null)
            {
                return ValueTask.FromException<byte[]>(DownloadException);
            }

            return ValueTask.FromResult(_messageDataBySequence[message.SequenceNumber]);
        }

        public ValueTask DeleteMessageAsync(
            ExternalFetchRemoteMessage message,
            CancellationToken cancellationToken)
        {
            DeletedUids.Add(message.Uid);
            if (DeleteException is not null)
            {
                return ValueTask.FromException(DeleteException);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }

    private sealed class FakeSmtpMessageReceiver : ISmtpMessageReceiver
    {
        private readonly SmtpReceiveResult _result;

        public FakeSmtpMessageReceiver()
            : this(SmtpReceiveResult.Success())
        {
        }

        public FakeSmtpMessageReceiver(SmtpReceiveResult result)
        {
            _result = result;
        }

        public List<SmtpReceiveRequest> Requests { get; } = [];

        public ValueTask<SmtpReceiveResult> ReceiveAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class FakeExternalAccountDownloadScriptExecutor : IExternalAccountDownloadScriptExecutor
    {
        private readonly Func<ExternalAccountDownloadScriptExecutionRequest, ExternalAccountDownloadScriptExecutionResult> _execute;

        public FakeExternalAccountDownloadScriptExecutor(
            Func<ExternalAccountDownloadScriptExecutionRequest, ExternalAccountDownloadScriptExecutionResult> execute)
        {
            _execute = execute;
        }

        public List<ExternalAccountDownloadScriptExecutionRequest> Requests { get; } = [];

        public ExternalAccountDownloadScriptExecutionResult Execute(
            ExternalAccountDownloadScriptExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return _execute(request);
        }
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

    private sealed class FakeSmtpRecipientValidator : ISmtpRecipientValidator
    {
        private readonly Func<SmtpRecipientValidationRequest, SmtpRecipientValidationResult> _validate;

        public FakeSmtpRecipientValidator(
            Func<SmtpRecipientValidationRequest, SmtpRecipientValidationResult> validate)
        {
            _validate = validate;
        }

        public List<SmtpRecipientValidationRequest> Requests { get; } = [];

        public ValueTask<SmtpRecipientValidationResult> ValidateAsync(
            SmtpRecipientValidationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(_validate(request));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() =>
            _utcNow;
    }
}
