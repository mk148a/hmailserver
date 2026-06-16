using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpRemoteDeliveryClientTests
{
    [TestMethod]
    public async Task SendAsync_SendsMailTransactionAndDotStuffsData()
    {
        var transport = new ScriptedSmtpTransport(
            "220 mx.example ESMTP\r\n" +
            "250-mx.example\r\n" +
            "250 SIZE 10485760\r\n" +
            "250 sender ok\r\n" +
            "250 recipient ok\r\n" +
            "354 go ahead\r\n" +
            "250 queued\r\n" +
            "221 bye\r\n");
        var client = new SmtpRemoteDeliveryClient(new FakeTransportFactory(transport));
        var request = new RemoteSmtpSendRequest(
            new RemoteSmtpEndpoint("mx.example", 25, RemoteSmtpConnectionSecurity.None),
            "mail.local.test",
            "sender@example.test",
            ["recipient@example.net"],
            "Subject: Test\r\n.body line\r\n"u8.ToArray());

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        var output = transport.GetClientText();
        StringAssert.Contains(output, "EHLO mail.local.test\r\n");
        StringAssert.Contains(output, "MAIL FROM:<sender@example.test>\r\n");
        StringAssert.Contains(output, "RCPT TO:<recipient@example.net>\r\n");
        StringAssert.Contains(output, "DATA\r\n");
        StringAssert.Contains(output, "Subject: Test\r\n..body line\r\n.\r\n");
        StringAssert.Contains(output, "QUIT\r\n");
    }

    [TestMethod]
    public async Task SendAsync_AuthenticatesWithLoginWhenEndpointRequiresAuth()
    {
        var transport = new ScriptedSmtpTransport(
            "220 mx.example ESMTP\r\n" +
            "250-mx.example\r\n" +
            "250 AUTH LOGIN\r\n" +
            "334 VXNlcm5hbWU6\r\n" +
            "334 UGFzc3dvcmQ6\r\n" +
            "235 authenticated\r\n" +
            "250 sender ok\r\n" +
            "250 recipient ok\r\n" +
            "354 go ahead\r\n" +
            "250 queued\r\n" +
            "221 bye\r\n");
        var client = new SmtpRemoteDeliveryClient(new FakeTransportFactory(transport));
        var request = new RemoteSmtpSendRequest(
            new RemoteSmtpEndpoint(
                "mx.example",
                25,
                RemoteSmtpConnectionSecurity.None,
                RequiresAuthentication: true,
                AuthenticationUsername: "relay-user",
                AuthenticationPassword: "secret"),
            "mail.local.test",
            "sender@example.test",
            ["recipient@example.net"],
            "Subject: Test\r\n\r\nHello\r\n"u8.ToArray());

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        var output = transport.GetClientText();
        StringAssert.Contains(output, "AUTH LOGIN\r\n");
        StringAssert.Contains(output, Convert.ToBase64String(Encoding.UTF8.GetBytes("relay-user")) + "\r\n");
        StringAssert.Contains(output, Convert.ToBase64String(Encoding.UTF8.GetBytes("secret")) + "\r\n");
    }

    [TestMethod]
    public async Task SendAsync_ClassifiesFiveHundredRecipientReplyAsPermanentFailure()
    {
        var transport = new ScriptedSmtpTransport(
            "220 mx.example ESMTP\r\n" +
            "250 mx.example\r\n" +
            "250 sender ok\r\n" +
            "550 no such user\r\n");
        var client = new SmtpRemoteDeliveryClient(new FakeTransportFactory(transport));
        var request = new RemoteSmtpSendRequest(
            new RemoteSmtpEndpoint("mx.example", 25, RemoteSmtpConnectionSecurity.None),
            "mail.local.test",
            "sender@example.test",
            ["recipient@example.net"],
            "Subject: Test\r\n\r\nHello\r\n"u8.ToArray());

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DeliveryFailureKind.Permanent, result.FailureKind);
        StringAssert.Contains(result.Error, "550");
    }

    private sealed class FakeTransportFactory : IRemoteSmtpTransportFactory
    {
        private readonly ScriptedSmtpTransport _transport;

        public FakeTransportFactory(ScriptedSmtpTransport transport)
        {
            _transport = transport;
        }

        public ValueTask<IRemoteSmtpTransport> ConnectAsync(
            RemoteSmtpEndpoint endpoint,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IRemoteSmtpTransport>(_transport);
    }

    private sealed class ScriptedSmtpTransport : IRemoteSmtpTransport
    {
        private readonly ScriptedSmtpStream _stream;

        public ScriptedSmtpTransport(string serverScript)
        {
            _stream = new ScriptedSmtpStream(serverScript);
        }

        public Stream Stream => _stream;

        public ValueTask UpgradeToTlsAsync(
            string targetHost,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;

        public string GetClientText() => _stream.GetClientText();
    }

    private sealed class ScriptedSmtpStream : Stream
    {
        private readonly MemoryStream _serverBytes;
        private readonly MemoryStream _clientBytes = new();

        public ScriptedSmtpStream(string serverScript)
        {
            _serverBytes = new MemoryStream(Encoding.ASCII.GetBytes(serverScript));
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

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) =>
            _serverBytes.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_serverBytes.Read(buffer.Span));

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            _clientBytes.Write(buffer, offset, count);

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _clientBytes.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public string GetClientText() =>
            Encoding.ASCII.GetString(_clientBytes.ToArray());
    }
}
