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
        StringAssert.Contains(Encoding.ASCII.GetString(receiver.Requests[0].MessageData), "X-Script: uid-1\r\n");
        Assert.AreEqual("uid-1", script.Requests.Single().RemoteUid);
        Assert.IsNotNull(script.Requests.Single().MessageData);
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
    public async Task RunBatchAsync_ReleasesLeaseWhenReceiverRejects()
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
        Assert.AreEqual(77, store.ReleasedFetchAccountIds.Single());
        Assert.AreEqual(0, store.AddedUids.Count);
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

    private static ExternalFetchProcessor CreateProcessor(
        FakeExternalFetchAccountStore store,
        FakeExternalFetchSession session,
        FakeSmtpMessageReceiver receiver,
        IExternalAccountDownloadScriptExecutor? scriptExecutor = null,
        IMessageAntivirusScanner? antivirusScanner = null,
        ISmtpRecipientValidator? recipientValidator = null) =>
        new(
            store,
            new FakeExternalFetchSessionFactory(session),
            receiver,
            scriptExecutor,
            antivirusScanner,
            recipientValidator,
            timeProvider: new FixedTimeProvider(DateTimeOffset.Parse("2026-01-10T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)));

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

        public ValueTask ResetLocksAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

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

        public FakeExternalFetchSessionFactory(FakeExternalFetchSession session)
        {
            _session = session;
        }

        public ValueTask<IExternalFetchSession> ConnectAsync(
            ExternalFetchAccountLease account,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IExternalFetchSession>(_session);
    }

    private sealed class FakeExternalFetchSession : IExternalFetchSession
    {
        private readonly ExternalFetchRemoteMessage _message;
        private readonly byte[] _messageData;

        public FakeExternalFetchSession(
            ExternalFetchRemoteMessage message,
            byte[] messageData)
        {
            _message = message;
            _messageData = messageData;
        }

        public List<string> DeletedUids { get; } = [];

        public ValueTask<IReadOnlyList<ExternalFetchRemoteMessage>> ListMessagesAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ExternalFetchRemoteMessage>>([_message]);

        public ValueTask<byte[]> DownloadMessageAsync(
            ExternalFetchRemoteMessage message,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(_messageData);

        public ValueTask DeleteMessageAsync(
            ExternalFetchRemoteMessage message,
            CancellationToken cancellationToken)
        {
            DeletedUids.Add(message.Uid);
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
