using System.Net;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Pop3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class Pop3SessionTests
{
    [TestMethod]
    public async Task RunAsync_HandlesLegacyHelpBeforeAuthentication()
    {
        await using var stream = new DuplexMemoryStream("HELP\r\nQUIT\r\n");
        var session = new Pop3Session(
            new FakeAccountAuthenticator(),
            new FakePop3MailboxStore(Array.Empty<StoredMessage>()));

        await session.RunAsync(stream, CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "+OK Normal POP3 commands allowed\r\n");
    }

    [TestMethod]
    public async Task RunAsync_HandlesAuthenticatedMailboxCommands()
    {
        var messageOne = Encoding.ASCII.GetBytes("Subject: one\r\n\r\n.Line\r\n..Two\r\nEnd");
        var messageTwo = Encoding.ASCII.GetBytes("Subject: two\r\n\r\nBody\r\n");
        var store = new FakePop3MailboxStore(
            new[]
            {
                new StoredMessage(101, "uid-101", messageOne),
                new StoredMessage(102, "uid-102", messageTwo)
            });
        await using var stream = new DuplexMemoryStream(
            "USER user@example.test\r\n" +
            "PASS secret\r\n" +
            "STAT\r\n" +
            "LIST\r\n" +
            "UIDL 2\r\n" +
            "RETR 1\r\n" +
            "DELE 1\r\n" +
            "STAT\r\n" +
            "RSET\r\n" +
            "STAT\r\n" +
            "DELE 2\r\n" +
            "QUIT\r\n");
        var session = new Pop3Session(new FakeAccountAuthenticator(), store);

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "+OK hMailServer .NET 10 POP3 ready\r\n");
        StringAssert.Contains(output, "+OK User accepted\r\n");
        StringAssert.Contains(output, "+OK Mailbox locked and ready\r\n");
        StringAssert.Contains(output, $"+OK 2 {messageOne.Length + messageTwo.Length}\r\n");
        StringAssert.Contains(output, $"+OK Mailbox scan listing follows\r\n1 {messageOne.Length}\r\n2 {messageTwo.Length}\r\n.\r\n");
        StringAssert.Contains(output, "+OK 2 uid-102\r\n");
        StringAssert.Contains(
            output,
            $"+OK {messageOne.Length} octets\r\nSubject: one\r\n\r\n..Line\r\n...Two\r\nEnd\r\n.\r\n");
        StringAssert.Contains(output, $"+OK 1 {messageTwo.Length}\r\n");
        StringAssert.Contains(output, "+OK Reset state\r\n");
        StringAssert.Contains(output, "+OK hMailServer POP3 server signing off\r\n");
        CollectionAssert.AreEqual(new long[] { 102 }, store.DeletedMessageIds.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_RejectsTransactionCommandsBeforeAuthentication()
    {
        var store = new FakePop3MailboxStore(Array.Empty<StoredMessage>());
        await using var stream = new DuplexMemoryStream(
            "STAT\r\n" +
            "PASS secret\r\n" +
            "USER user@example.test\r\n" +
            "PASS wrong\r\n" +
            "LIST\r\n" +
            "QUIT\r\n");
        var session = new Pop3Session(new FakeAccountAuthenticator(), store);

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "-ERR Authentication required\r\n");
        StringAssert.Contains(output, "-ERR USER required\r\n");
        StringAssert.Contains(output, "-ERR Invalid user name or password.\r\n");
        Assert.AreEqual(0, store.ListCallCount);
        CollectionAssert.AreEqual(Array.Empty<long>(), store.DeletedMessageIds.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_RecordsAutoBanFailureAndDisconnectsWhenThresholdReached()
    {
        var store = new FakePop3MailboxStore(Array.Empty<StoredMessage>());
        await using var stream = new DuplexMemoryStream(
            "USER user@example.test\r\n" +
            "PASS wrong\r\n" +
            "STAT\r\n");
        var autoBanRecorder = new CapturingAutoBanRecorder(disconnect: true);
        var session = new Pop3Session(
            new FakeAccountAuthenticator(),
            store,
            autoBanLogonFailureRecorder: autoBanRecorder);

        await session.RunAsync(
            stream,
            new Pop3SessionConnectionContext(
                ClientIPAddress: "203.0.113.14",
                ClientPort: 1110,
                SessionId: 44),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "-ERR Invalid user name or password.\r\n");
        Assert.IsFalse(output.Contains("-ERR Authentication required\r\n", StringComparison.Ordinal));
        var failure = autoBanRecorder.Failures.Single();
        Assert.AreEqual(IPAddress.Parse("203.0.113.14"), failure.ClientAddress);
        Assert.AreEqual("user@example.test", failure.Username);
        Assert.AreEqual(0, store.ListCallCount);
    }

    [TestMethod]
    public async Task RunAsync_UsesInjectedBoundaryWithPop3CallerAndRemoteAddress()
    {
        var store = new FakePop3MailboxStore(Array.Empty<StoredMessage>());
        var boundary = new CapturingClientAwareAuthenticationService(
            ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(77, "user@example.test")));
        await using var stream = new DuplexMemoryStream(
            "USER user@example.test\r\nPASS secret\r\nQUIT\r\n");
        var session = new Pop3Session(
            new FakeAccountAuthenticator(),
            store,
            clientAwareAuthenticationService: boundary);

        await session.RunAsync(
            stream,
            new Pop3SessionConnectionContext(
                ClientIPAddress: "203.0.113.32",
                ClientPort: 1110,
                SessionId: 47),
            CancellationToken.None);

        StringAssert.Contains(stream.GetOutputText(), "+OK Mailbox locked and ready\r\n");
        Assert.IsNotNull(boundary.LastRequest);
        Assert.AreEqual("user@example.test", boundary.LastRequest.Username);
        Assert.AreEqual("secret", boundary.LastRequest.Password);
        Assert.AreEqual(IPAddress.Parse("203.0.113.32"), boundary.LastRequest.ClientAddress);
        Assert.AreEqual(ClientAuthenticationCaller.Pop3, boundary.LastRequest.Caller);
    }

    [TestMethod]
    public async Task RunAsync_UsesInjectedFailureAndDisconnectsForPop3()
    {
        var store = new FakePop3MailboxStore(Array.Empty<StoredMessage>());
        var boundary = new CapturingClientAwareAuthenticationService(
            ImapAuthenticationResult.Failure("Injected authentication failure."),
            disconnect: true);
        await using var stream = new DuplexMemoryStream(
            "USER user@example.test\r\nPASS wrong\r\nSTAT\r\n");
        var session = new Pop3Session(
            new FakeAccountAuthenticator(),
            store,
            clientAwareAuthenticationService: boundary);

        await session.RunAsync(
            stream,
            new Pop3SessionConnectionContext(
                ClientIPAddress: "203.0.113.35",
                ClientPort: 1110,
                SessionId: 49),
            CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "-ERR Injected authentication failure.\r\n");
        Assert.IsFalse(output.Contains("+OK Mailbox locked and ready\r\n", StringComparison.Ordinal));
        Assert.IsFalse(output.Contains("+OK 0 messages\r\n", StringComparison.Ordinal));
        Assert.AreEqual(0, store.ListCallCount);
        Assert.IsNotNull(boundary.LastRequest);
        Assert.AreEqual(ClientAuthenticationCaller.Pop3, boundary.LastRequest.Caller);
    }

    [TestMethod]
    public async Task RunAsync_RunsOnClientLogonAfterSuccessfulPass()
    {
        var store = new FakePop3MailboxStore(Array.Empty<StoredMessage>());
        await using var stream = new DuplexMemoryStream(
            "USER user@example.test\r\n" +
            "PASS secret\r\n" +
            "QUIT\r\n");
        var eventExecutor = new CapturingEventScriptExecutor();
        var session = new Pop3Session(
            new FakeAccountAuthenticator(),
            store,
            eventScriptExecutor: eventExecutor);

        await session.RunAsync(
            stream,
            new Pop3SessionConnectionContext(
                ClientIPAddress: "203.0.113.20",
                ClientPort: 995,
                SessionId: 45,
                IsEncryptedConnection: true),
            CancellationToken.None);

        var request = eventExecutor.Requests.Single();
        Assert.AreEqual("OnClientLogon", request.EventName);
        Assert.AreEqual(SmtpEventScriptArgumentShape.ClientOnly, request.ArgumentShape);
        Assert.AreEqual("user@example.test", request.Client.Username);
        Assert.AreEqual("203.0.113.20", request.Client.IPAddress);
        Assert.AreEqual(995, request.Client.Port);
        Assert.AreEqual(45, request.Client.SessionId);
        Assert.IsTrue(request.Client.IsAuthenticated);
        Assert.IsTrue(request.Client.IsEncryptedConnection);
    }

    [TestMethod]
    public async Task RunAsync_RunsOnClientLogonAfterFailedPass()
    {
        var store = new FakePop3MailboxStore(Array.Empty<StoredMessage>());
        await using var stream = new DuplexMemoryStream(
            "USER user@example.test\r\n" +
            "PASS wrong\r\n" +
            "QUIT\r\n");
        var eventExecutor = new CapturingEventScriptExecutor();
        var session = new Pop3Session(
            new FakeAccountAuthenticator(),
            store,
            eventScriptExecutor: eventExecutor);

        await session.RunAsync(
            stream,
            new Pop3SessionConnectionContext(
                ClientIPAddress: "203.0.113.21",
                ClientPort: 110,
                SessionId: 46),
            CancellationToken.None);

        var request = eventExecutor.Requests.Single();
        Assert.AreEqual("OnClientLogon", request.EventName);
        Assert.AreEqual("user@example.test", request.Client.Username);
        Assert.AreEqual("203.0.113.21", request.Client.IPAddress);
        Assert.AreEqual(110, request.Client.Port);
        Assert.AreEqual(46, request.Client.SessionId);
        Assert.IsFalse(request.Client.IsAuthenticated);
        Assert.IsFalse(request.Client.IsEncryptedConnection);
        Assert.AreEqual(0, store.ListCallCount);
    }

    [TestMethod]
    public async Task RunAsync_DeletesAreInvisibleUntilResetOrQuitCommit()
    {
        var store = new FakePop3MailboxStore(
            new[]
            {
                new StoredMessage(201, "uid-201", Encoding.ASCII.GetBytes("Subject: one\r\n\r\nBody\r\n")),
                new StoredMessage(202, "uid-202", Encoding.ASCII.GetBytes("Subject: two\r\n\r\nBody\r\n"))
            });
        await using var stream = new DuplexMemoryStream(
            "USER user@example.test\r\n" +
            "PASS secret\r\n" +
            "DELE 1\r\n" +
            "LIST 1\r\n" +
            "UIDL\r\n" +
            "RSET\r\n" +
            "LIST 1\r\n" +
            "QUIT\r\n");
        var session = new Pop3Session(new FakeAccountAuthenticator(), store);

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "+OK Message deleted\r\n");
        StringAssert.Contains(output, "-ERR No such message\r\n");
        StringAssert.Contains(output, "+OK Unique-id listing follows\r\n2 uid-202\r\n.\r\n");
        StringAssert.Contains(output, "+OK 1 22\r\n");
        CollectionAssert.AreEqual(Array.Empty<long>(), store.DeletedMessageIds.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_HandlesCapaAndTop()
    {
        var message = Encoding.ASCII.GetBytes("Subject: one\r\nX-Test: yes\r\n\r\n.Line\r\nSecond\r\nThird\r\n");
        var store = new FakePop3MailboxStore(
            new[]
            {
                new StoredMessage(301, "uid-301", message)
            });
        await using var stream = new DuplexMemoryStream(
            "CAPA\r\n" +
            "USER user@example.test\r\n" +
            "PASS secret\r\n" +
            "TOP 1 2\r\n" +
            "DELE 1\r\n" +
            "TOP 1 1\r\n" +
            "QUIT\r\n");
        var session = new Pop3Session(new FakeAccountAuthenticator(), store);

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "+OK Capability list follows\r\nUIDL\r\nTOP\r\nUSER\r\n.\r\n");
        StringAssert.Contains(
            output,
            $"+OK {message.Length} octets\r\nSubject: one\r\nX-Test: yes\r\n\r\n..Line\r\nSecond\r\n.\r\n");
        StringAssert.Contains(output, "-ERR No such message\r\n");
        CollectionAssert.AreEqual(new long[] { 301 }, store.DeletedMessageIds.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_RejectsPassWhenMailboxIsAlreadyLocked()
    {
        var store = new FakePop3MailboxStore(Array.Empty<StoredMessage>());
        var lockManager = new RejectingMailboxLockManager();
        await using var stream = new DuplexMemoryStream(
            "USER user@example.test\r\n" +
            "PASS secret\r\n" +
            "STAT\r\n" +
            "QUIT\r\n");
        var session = new Pop3Session(
            new FakeAccountAuthenticator(),
            store,
            mailboxLockManager: lockManager);

        await session.RunAsync(stream, CancellationToken.None);

        var output = stream.GetOutputText();
        StringAssert.Contains(output, "-ERR Your mailbox is already locked\r\n");
        StringAssert.Contains(output, "-ERR Authentication required\r\n");
        Assert.AreEqual(1, lockManager.AttemptCount);
        Assert.AreEqual(0, store.ListCallCount);
    }

    [TestMethod]
    public async Task RunAsync_ReleasesMailboxLockWhenSessionEnds()
    {
        var store = new FakePop3MailboxStore(Array.Empty<StoredMessage>());
        var lockManager = new InMemoryPop3MailboxLockManager();
        await using var stream = new DuplexMemoryStream(
            "USER user@example.test\r\n" +
            "PASS secret\r\n" +
            "QUIT\r\n");
        var session = new Pop3Session(
            new FakeAccountAuthenticator(),
            store,
            mailboxLockManager: lockManager);

        await session.RunAsync(stream, CancellationToken.None);

        var lease = await lockManager
            .TryAcquireAsync(new ImapAuthenticatedAccount(77, "user@example.test"), CancellationToken.None)
            .ConfigureAwait(false);
        Assert.IsNotNull(lease);
        await lease.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class DuplexMemoryStream : Stream
    {
        private readonly MemoryStream _input;
        private readonly MemoryStream _output = new();

        public DuplexMemoryStream(string input)
        {
            _input = new MemoryStream(Encoding.ASCII.GetBytes(input));
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public string GetOutputText() => Encoding.ASCII.GetString(_output.ToArray());

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _input.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_input.Read(buffer.Span));
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            _output.Write(buffer, offset, count);

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _output.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeAccountAuthenticator : IImapAccountAuthenticator
    {
        public ValueTask<ImapAuthenticationResult> AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            if (username == "user@example.test" && password == "secret")
            {
                return ValueTask.FromResult(
                    ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(77, username)));
            }

            return ValueTask.FromResult(ImapAuthenticationResult.Failure("Invalid user name or password."));
        }
    }

    private sealed class CapturingAutoBanRecorder : IAutoBanLogonFailureRecorder
    {
        private readonly bool _disconnect;

        public CapturingAutoBanRecorder(bool disconnect)
        {
            _disconnect = disconnect;
        }

        public List<(IPAddress ClientAddress, string Username)> Failures { get; } = [];

        public ValueTask<AutoBanLogonFailureResult> RecordFailureAsync(
            IPAddress clientAddress,
            string username,
            CancellationToken cancellationToken)
        {
            Failures.Add((clientAddress, username));
            return ValueTask.FromResult(
                new AutoBanLogonFailureResult(
                    Enabled: true,
                    FailureCount: Failures.Count,
                    Disconnect: _disconnect,
                    RangeCreated: _disconnect));
        }

        public ValueTask ClearOldFailuresAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class CapturingEventScriptExecutor : ISmtpEventScriptExecutor
    {
        public List<SmtpEventScriptExecutionRequest> Requests { get; } = [];

        public SmtpRuleScriptExecutionResult Execute(
            SmtpEventScriptExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return SmtpRuleScriptExecutionResult.Continue();
        }
    }

    private sealed class FakePop3MailboxStore : IPop3MailboxStore
    {
        private readonly Dictionary<long, StoredMessage> _messages;

        public FakePop3MailboxStore(IEnumerable<StoredMessage> messages)
        {
            _messages = messages.ToDictionary(message => message.MessageId);
        }

        public List<long> DeletedMessageIds { get; } = new();

        public int ListCallCount { get; private set; }

        public ValueTask<IReadOnlyList<Pop3MessageListing>> ListMessagesAsync(
            ImapAuthenticatedAccount account,
            CancellationToken cancellationToken)
        {
            ListCallCount++;
            IReadOnlyList<Pop3MessageListing> result = _messages
                .Values
                .OrderBy(message => message.MessageId)
                .Select(message => new Pop3MessageListing(
                    message.MessageId,
                    message.Uid,
                    message.Content.Length))
                .ToArray();
            return ValueTask.FromResult(result);
        }

        public ValueTask<Stream> OpenMessageAsync(
            ImapAuthenticatedAccount account,
            long messageId,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<Stream>(new MemoryStream(_messages[messageId].Content, writable: false));
        }

        public ValueTask DeleteMessagesAsync(
            ImapAuthenticatedAccount account,
            IReadOnlyCollection<long> messageIds,
            CancellationToken cancellationToken)
        {
            DeletedMessageIds.AddRange(messageIds);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RejectingMailboxLockManager : IPop3MailboxLockManager
    {
        public int AttemptCount { get; private set; }

        public void Unlock(int accountId)
        {
        }

        public ValueTask<IAsyncDisposable?> TryAcquireAsync(
            ImapAuthenticatedAccount account,
            CancellationToken cancellationToken)
        {
            AttemptCount++;
            return ValueTask.FromResult<IAsyncDisposable?>(null);
        }
    }

    private sealed record StoredMessage(
        long MessageId,
        string Uid,
        byte[] Content);
}
