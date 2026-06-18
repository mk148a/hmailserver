using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Pop3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class Pop3SessionTests
{
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

    private sealed record StoredMessage(
        long MessageId,
        string Uid,
        byte[] Content);
}
