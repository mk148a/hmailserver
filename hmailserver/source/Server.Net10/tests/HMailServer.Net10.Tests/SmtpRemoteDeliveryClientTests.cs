using System.Net;
using System.Net.Sockets;
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

    [TestMethod]
    public async Task SendAsync_TransientFirstCandidateTriesSecondCandidate()
    {
        var first = new ScriptedSmtpTransport(
            "220 first.example ESMTP\r\n" +
            "250 first.example\r\n" +
            "451 sender temporarily deferred\r\n");
        var second = CreateSuccessfulTransport();
        var factory = new FakeTransportFactory(first, second);
        var client = new SmtpRemoteDeliveryClient(factory);
        var request = CreateRequest(RemoteSmtpConnectionSecurity.None) with
        {
            Endpoint = new RemoteSmtpEndpoint(
                "first.example",
                25,
                RemoteSmtpConnectionSecurity.None,
                HostCandidates: ["first.example", "second.example"])
        };

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        CollectionAssert.AreEqual(
            new[] { "first.example", "second.example" },
            factory.Endpoints.Select(static endpoint => endpoint.Host).ToArray());
    }

    [TestMethod]
    public async Task SendAsync_PermanentFirstCandidateStopsFailover()
    {
        var first = new ScriptedSmtpTransport(
            "220 first.example ESMTP\r\n" +
            "250 first.example\r\n" +
            "550 sender denied\r\n");
        var second = CreateSuccessfulTransport();
        var factory = new FakeTransportFactory(first, second);
        var client = new SmtpRemoteDeliveryClient(factory);
        var request = CreateRequest(RemoteSmtpConnectionSecurity.None) with
        {
            Endpoint = new RemoteSmtpEndpoint(
                "first.example",
                25,
                RemoteSmtpConnectionSecurity.None,
                HostCandidates: ["first.example", "second.example"])
        };

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DeliveryFailureKind.Permanent, result.FailureKind);
        Assert.AreEqual(1, factory.Endpoints.Count);
    }

    [TestMethod]
    public async Task SendAsync_AllTransientCandidatesReturnOneTransientResult()
    {
        var first = new ScriptedSmtpTransport(
            "220 first.example ESMTP\r\n" +
            "250 first.example\r\n" +
            "451 sender temporarily deferred\r\n");
        var second = new ScriptedSmtpTransport(
            "220 second.example ESMTP\r\n" +
            "250 second.example\r\n" +
            "421 sender temporarily deferred\r\n");
        var factory = new FakeTransportFactory(first, second);
        var client = new SmtpRemoteDeliveryClient(factory);
        var request = CreateRequest(RemoteSmtpConnectionSecurity.None) with
        {
            Endpoint = new RemoteSmtpEndpoint(
                "first.example",
                25,
                RemoteSmtpConnectionSecurity.None,
                HostCandidates: ["first.example", "second.example"])
        };

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DeliveryFailureKind.Transient, result.FailureKind);
        Assert.AreEqual(2, factory.Endpoints.Count);
    }

    [TestMethod]
    public async Task SendAsync_DoesNotFailOverAfterARecipientWasAccepted()
    {
        var first = new ScriptedSmtpTransport(
            "220 first.example ESMTP\r\n" +
            "250 first.example\r\n" +
            "250 sender ok\r\n" +
            "250 first recipient ok\r\n" +
            "451 second recipient temporarily deferred\r\n");
        var second = CreateSuccessfulTransport();
        var factory = new FakeTransportFactory(first, second);
        var client = new SmtpRemoteDeliveryClient(factory);
        var request = CreateRequest(RemoteSmtpConnectionSecurity.None) with
        {
            Endpoint = new RemoteSmtpEndpoint(
                "first.example",
                25,
                RemoteSmtpConnectionSecurity.None,
                HostCandidates: ["first.example", "second.example"]),
            RecipientAddresses = ["first@example.net", "second@example.net"]
        };

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DeliveryFailureKind.Transient, result.FailureKind);
        Assert.IsFalse(result.TryNextEndpoint);
        Assert.AreEqual(1, factory.Endpoints.Count);
    }

    [TestMethod]
    public async Task SendAsync_OptionalStartTlsStaysPlaintextWhenNotAdvertised()
    {
        var transport = new ScriptedSmtpTransport(
            "220 mx.example ESMTP\r\n" +
            "250 mx.example\r\n" +
            "250 sender ok\r\n" +
            "250 recipient ok\r\n" +
            "354 go ahead\r\n" +
            "250 queued\r\n" +
            "221 bye\r\n");
        var factory = new FakeTransportFactory(transport);
        var client = new SmtpRemoteDeliveryClient(factory);
        var request = CreateRequest(RemoteSmtpConnectionSecurity.StartTlsOptional);

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, factory.Endpoints.Count);
        Assert.AreEqual(0, transport.UpgradeCallCount);
        Assert.IsFalse(transport.GetClientText().Contains("STARTTLS", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SendAsync_OptionalStartTlsDoesNotSendAuthenticatedCredentialsWithoutTls()
    {
        var transport = new ScriptedSmtpTransport(
            "220 mx.example ESMTP\r\n" +
            "250 mx.example\r\n");
        var factory = new FakeTransportFactory(transport);
        var client = new SmtpRemoteDeliveryClient(factory);
        var request = CreateRequest(RemoteSmtpConnectionSecurity.StartTlsOptional) with
        {
            Endpoint = new RemoteSmtpEndpoint(
                "mx.example",
                25,
                RemoteSmtpConnectionSecurity.StartTlsOptional,
                RequiresAuthentication: true,
                AuthenticationUsername: "user",
                AuthenticationPassword: "secret")
        };

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "did not advertise STARTTLS");
        Assert.AreEqual(1, factory.Endpoints.Count);
        Assert.IsFalse(transport.GetClientText().Contains("AUTH LOGIN", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SendAsync_OptionalStartTlsNegativeReplyRetriesOnceWithoutTls()
    {
        var first = new ScriptedSmtpTransport(
            "220 mx.example ESMTP\r\n" +
            "250-mx.example\r\n" +
            "250 STARTTLS\r\n" +
            "454 TLS unavailable\r\n");
        var second = CreateSuccessfulTransport();
        var factory = new FakeTransportFactory(first, second);
        var client = new SmtpRemoteDeliveryClient(factory);

        var result = await client.SendAsync(CreateRequest(RemoteSmtpConnectionSecurity.StartTlsOptional), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2, factory.Endpoints.Count);
        Assert.AreEqual(RemoteSmtpConnectionSecurity.StartTlsOptional, factory.Endpoints[0].ConnectionSecurity);
        Assert.AreEqual(RemoteSmtpConnectionSecurity.None, factory.Endpoints[1].ConnectionSecurity);
        StringAssert.Contains(first.GetClientText(), "STARTTLS\r\n");
        Assert.IsFalse(second.GetClientText().Contains("STARTTLS", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SendAsync_OptionalStartTlsHandshakeFailureDoesNotDowngrade()
    {
        var first = new ScriptedSmtpTransport(
            "220 mx.example ESMTP\r\n" +
            "250-mx.example\r\n" +
            "250 STARTTLS\r\n" +
            "220 ready to start TLS\r\n",
            new InvalidOperationException("TLS handshake failed"));
        var factory = new FakeTransportFactory(first);
        var client = new SmtpRemoteDeliveryClient(factory);

        var result = await client.SendAsync(CreateRequest(RemoteSmtpConnectionSecurity.StartTlsOptional), CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DeliveryFailureKind.Transient, result.FailureKind);
        Assert.AreEqual(1, factory.Endpoints.Count);
        Assert.AreEqual(1, first.UpgradeCallCount);
        Assert.IsFalse(first.VerifyRemoteSslCertificate);
    }

    [TestMethod]
    public async Task SendAsync_OptionalStartTlsDoesNotFallbackAfterTlsSucceeds()
    {
        var transport = new ScriptedSmtpTransport(
            "220 mx.example ESMTP\r\n" +
            "250-mx.example\r\n" +
            "250 STARTTLS\r\n" +
            "220 ready to start TLS\r\n" +
            "250 mx.example\r\n" +
            "550 sender denied\r\n");
        var factory = new FakeTransportFactory(transport);
        var client = new SmtpRemoteDeliveryClient(factory);

        var result = await client.SendAsync(CreateRequest(RemoteSmtpConnectionSecurity.StartTlsOptional), CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DeliveryFailureKind.Permanent, result.FailureKind);
        Assert.AreEqual(1, factory.Endpoints.Count);
        Assert.AreEqual(1, transport.UpgradeCallCount);
    }

    [TestMethod]
    public async Task SendAsync_RequiredStartTlsStillFailsWhenNotAdvertised()
    {
        var transport = new ScriptedSmtpTransport(
            "220 mx.example ESMTP\r\n" +
            "250 mx.example\r\n");
        var factory = new FakeTransportFactory(transport);
        var client = new SmtpRemoteDeliveryClient(factory);

        var result = await client.SendAsync(CreateRequest(RemoteSmtpConnectionSecurity.StartTlsRequired), CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "did not advertise STARTTLS");
        Assert.AreEqual(1, factory.Endpoints.Count);
    }

    [TestMethod]
    public async Task SendAsync_SslStillUpgradesBeforeGreetingWithoutStartTlsFallback()
    {
        var transport = CreateSuccessfulTransport();
        var factory = new FakeTransportFactory(transport);
        var client = new SmtpRemoteDeliveryClient(factory);

        var result = await client.SendAsync(CreateRequest(RemoteSmtpConnectionSecurity.Ssl), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, transport.UpgradeCallCount);
        Assert.IsTrue(transport.VerifyRemoteSslCertificate);
        Assert.IsFalse(transport.GetClientText().Contains("STARTTLS", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SendAsync_SslHonorsExplicitCertificateVerificationDisable()
    {
        var transport = CreateSuccessfulTransport();
        var factory = new FakeTransportFactory(transport);
        var client = new SmtpRemoteDeliveryClient(factory);
        var request = CreateRequest(RemoteSmtpConnectionSecurity.Ssl) with
        {
            Endpoint = new RemoteSmtpEndpoint(
                "mx.example",
                25,
                RemoteSmtpConnectionSecurity.Ssl,
                VerifyRemoteSslCertificate: false)
        };

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.IsFalse(transport.VerifyRemoteSslCertificate);
    }

    [TestMethod]
    public async Task SendAsync_UsesConnectionAddressForTransportAndHostForTls()
    {
        var transport = CreateSuccessfulTransport();
        var factory = new FakeTransportFactory(transport);
        var client = new SmtpRemoteDeliveryClient(factory);
        var request = CreateRequest(RemoteSmtpConnectionSecurity.Ssl) with
        {
            Endpoint = new RemoteSmtpEndpoint(
                "relay.example",
                25,
                RemoteSmtpConnectionSecurity.Ssl,
                ConnectionAddress: "192.0.2.1")
        };

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("192.0.2.1", factory.Endpoints.Single().ConnectionAddress);
        Assert.AreEqual("relay.example", transport.TargetHost);
    }

    [TestMethod]
    public async Task TcpTransportFactory_ConnectsUsingConnectionAddress()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptedTask = listener.AcceptTcpClientAsync();
        await using var transport = await new TcpRemoteSmtpTransportFactory().ConnectAsync(
            new RemoteSmtpEndpoint(
                "unresolvable.invalid",
                port,
                RemoteSmtpConnectionSecurity.None,
                ConnectionAddress: IPAddress.Loopback.ToString()),
            CancellationToken.None);
        using var accepted = await acceptedTask;
    }

    [TestMethod]
    public async Task TcpTransportFactory_RejectsDnsDerivedLocalListeningEndpointBeforeConnect()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var localEndpoint = (IPEndPoint)listener.LocalEndpoint;
        var policy = new RemoteSmtpLocalEndpointPolicy(() => [localEndpoint]);
        var endpoint = new RemoteSmtpEndpoint(
            "local-alias.example",
            localEndpoint.Port,
            RemoteSmtpConnectionSecurity.None,
            ConnectionAddress: IPAddress.Loopback.ToString(),
            EnforceLocalEndpointGuard: true);

        await Assert.ThrowsExactlyAsync<RemoteSmtpLocalEndpointDeniedException>(() =>
            new TcpRemoteSmtpTransportFactory(policy)
                .ConnectAsync(endpoint, CancellationToken.None)
                .AsTask());
    }

    [TestMethod]
    public async Task SendAsync_ContinuesToNextCandidateAfterLocalEndpointDenial()
    {
        var second = CreateSuccessfulTransport();
        var factory = new FakeTransportFactory(second)
        {
            FirstException = new RemoteSmtpLocalEndpointDeniedException("local listener")
        };
        var client = new SmtpRemoteDeliveryClient(factory);
        var request = CreateRequest(RemoteSmtpConnectionSecurity.None) with
        {
            Endpoint = new RemoteSmtpEndpoint(
                "first.example",
                25,
                RemoteSmtpConnectionSecurity.None,
                HostCandidates: ["first.example", "second.example"])
        };

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2, factory.Endpoints.Count);
    }

    private static RemoteSmtpSendRequest CreateRequest(RemoteSmtpConnectionSecurity security) =>
        new(
            new RemoteSmtpEndpoint("mx.example", 25, security),
            "mail.local.test",
            "sender@example.test",
            ["recipient@example.net"],
            "Subject: Test\r\n\r\nHello\r\n"u8.ToArray());

    private static ScriptedSmtpTransport CreateSuccessfulTransport() =>
        new(
            "220 mx.example ESMTP\r\n" +
            "250 mx.example\r\n" +
            "250 sender ok\r\n" +
            "250 recipient ok\r\n" +
            "354 go ahead\r\n" +
            "250 queued\r\n" +
            "221 bye\r\n");

    private sealed class FakeTransportFactory : IRemoteSmtpTransportFactory
    {
        private readonly Queue<ScriptedSmtpTransport> _transports;

        public FakeTransportFactory(params ScriptedSmtpTransport[] transports)
        {
            _transports = new Queue<ScriptedSmtpTransport>(transports);
        }

        public List<RemoteSmtpEndpoint> Endpoints { get; } = [];

        public Exception? FirstException { get; init; }

        public ValueTask<IRemoteSmtpTransport> ConnectAsync(
            RemoteSmtpEndpoint endpoint,
            CancellationToken cancellationToken)
        {
            Endpoints.Add(endpoint);
            if (Endpoints.Count == 1 && FirstException is not null)
            {
                throw FirstException;
            }

            if (_transports.Count == 0)
            {
                throw new InvalidOperationException("No scripted transport remains.");
            }

            return ValueTask.FromResult<IRemoteSmtpTransport>(_transports.Dequeue());
        }
    }

    private sealed class ScriptedSmtpTransport : IRemoteSmtpTransport
    {
        private readonly ScriptedSmtpStream _stream;
        private readonly Exception? _upgradeException;

        public ScriptedSmtpTransport(string serverScript, Exception? upgradeException = null)
        {
            _stream = new ScriptedSmtpStream(serverScript);
            _upgradeException = upgradeException;
        }

        public Stream Stream => _stream;

        public int UpgradeCallCount { get; private set; }

        public bool VerifyRemoteSslCertificate { get; private set; }

        public string? TargetHost { get; private set; }

        public ValueTask UpgradeToTlsAsync(
            string targetHost,
            bool verifyRemoteSslCertificate,
            CancellationToken cancellationToken)
        {
            UpgradeCallCount++;
            TargetHost = targetHost;
            VerifyRemoteSslCertificate = verifyRemoteSslCertificate;
            if (_upgradeException is not null)
            {
                throw _upgradeException;
            }

            return ValueTask.CompletedTask;
        }

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
            _serverBytes.Read(buffer, offset, Math.Min(count, 1));

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_serverBytes.Read(buffer.Span[..Math.Min(buffer.Length, 1)]));

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
