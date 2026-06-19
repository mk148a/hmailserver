using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Pop3;
using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class Pop3TcpListenerTests
{
    [TestMethod]
    public async Task RunAsync_AcceptsTcpClientAndDispatchesPop3Commands()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = CreateListener(maxConcurrentConnections: 10);
        var runTask = listener.RunAsync(cts.Token);
        var endpoint = await listener.Started.WaitAsync(cts.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
        await using var stream = client.GetStream();
        using var reader = CreateReader(stream);
        await using var writer = CreateWriter(stream);

        Assert.AreEqual("+OK hMailServer .NET 10 POP3 ready", await ReadLineAsync(reader, cts.Token));
        await WriteLineAsync(writer, "USER user@example.test", cts.Token);
        Assert.AreEqual("+OK User accepted", await ReadLineAsync(reader, cts.Token));
        await WriteLineAsync(writer, "PASS secret", cts.Token);
        Assert.AreEqual("+OK Mailbox locked and ready", await ReadLineAsync(reader, cts.Token));
        await WriteLineAsync(writer, "STAT", cts.Token);
        Assert.AreEqual("+OK 1 22", await ReadLineAsync(reader, cts.Token));
        await WriteLineAsync(writer, "QUIT", cts.Token);
        Assert.AreEqual("+OK hMailServer POP3 server signing off", await ReadLineAsync(reader, cts.Token));

        await StopListenerAsync(runTask, cts);
    }

    [TestMethod]
    public async Task RunAsync_RepliesErrWhenConnectionLimitIsReached()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = CreateListener(maxConcurrentConnections: 1);
        var runTask = listener.RunAsync(cts.Token);
        var endpoint = await listener.Started.WaitAsync(cts.Token);

        using var firstClient = new TcpClient();
        await firstClient.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
        await using var firstStream = firstClient.GetStream();
        using var firstReader = CreateReader(firstStream);
        Assert.AreEqual("+OK hMailServer .NET 10 POP3 ready", await ReadLineAsync(firstReader, cts.Token));

        using var secondClient = new TcpClient();
        await secondClient.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
        await using var secondStream = secondClient.GetStream();
        using var secondReader = CreateReader(secondStream);
        Assert.AreEqual("-ERR Too many concurrent POP3 connections", await ReadLineAsync(secondReader, cts.Token));

        firstClient.Dispose();
        await StopListenerAsync(runTask, cts);
    }

    [TestMethod]
    public async Task RunAsync_AcceptsImplicitTlsClientWhenCertificateIsConfigured()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=hmailserver.test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var rawCertificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(
            rawCertificate.Export(X509ContentType.Pkcs12),
            null,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet,
            loaderLimits: null);
        var listener = CreateListener(
            maxConcurrentConnections: 10,
            streamFactory: new ImplicitTlsPop3ConnectionStreamFactory(
                () => TlsServerAuthenticationOptionsFactory.Create(certificate)));
        var runTask = listener.RunAsync(cts.Token);
        var endpoint = await listener.Started.WaitAsync(cts.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
        await using var tlsStream = new SslStream(
            client.GetStream(),
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: static (_, _, _, _) => true);
        await tlsStream.AuthenticateAsClientAsync(
            new SslClientAuthenticationOptions
            {
                TargetHost = "hmailserver.test",
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            },
            cts.Token).ConfigureAwait(false);
        using var reader = CreateReader(tlsStream);
        await using var writer = CreateWriter(tlsStream);

        Assert.AreEqual("+OK hMailServer .NET 10 POP3 ready", await ReadLineAsync(reader, cts.Token));
        await WriteLineAsync(writer, "QUIT", cts.Token);
        Assert.AreEqual("+OK hMailServer POP3 server signing off", await ReadLineAsync(reader, cts.Token));

        await StopListenerAsync(runTask, cts);
    }

    [TestMethod]
    public async Task RunAsync_RunsOnClientConnectBeforeGreetingAndCanReject()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        SmtpEventScriptExecutionRequest? capturedRequest = null;
        var listener = CreateListener(
            maxConcurrentConnections: 10,
            eventScriptExecutor: new FakeEventScriptExecutor(
                request =>
                {
                    capturedRequest = request;
                    return SmtpRuleScriptExecutionResult.Failure("554 Rejected");
                }));
        var runTask = listener.RunAsync(cts.Token);
        var endpoint = await listener.Started.WaitAsync(cts.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
        await using var stream = client.GetStream();
        using var reader = CreateReader(stream);

        Assert.IsNull(await ReadLineAsync(reader, cts.Token));
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("OnClientConnect", capturedRequest.EventName);
        Assert.AreEqual(SmtpEventScriptArgumentShape.ClientOnly, capturedRequest.ArgumentShape);
        Assert.AreEqual(IPAddress.Loopback.ToString(), capturedRequest.Client.IPAddress);
        Assert.IsGreaterThan(0, capturedRequest.Client.Port);
        Assert.IsGreaterThan(0, capturedRequest.Client.SessionId);

        await StopListenerAsync(runTask, cts);
    }

    private static Pop3TcpListener CreateListener(
        int maxConcurrentConnections,
        IPop3ConnectionStreamFactory? streamFactory = null,
        ISmtpEventScriptExecutor? eventScriptExecutor = null)
    {
        var message = Encoding.ASCII.GetBytes("Subject: one\r\n\r\nBody\r\n");
        var session = new Pop3Session(
            new FakeAccountAuthenticator(),
            new FakePop3MailboxStore(new StoredMessage(101, "101", message)));
        return new Pop3TcpListener(
            session,
            streamFactory ?? new PlainPop3ConnectionStreamFactory(),
            new Pop3TcpListenerOptions
            {
                ListenAddress = IPAddress.Loopback,
                Port = 0,
                Backlog = 16,
                MaxConcurrentConnections = maxConcurrentConnections,
                ShutdownGracePeriod = TimeSpan.FromSeconds(1)
            },
            eventScriptExecutor);
    }

    private static StreamReader CreateReader(Stream stream) =>
        new(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

    private static StreamWriter CreateWriter(Stream stream) =>
        new(stream, Encoding.ASCII, bufferSize: 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

    private static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken) =>
        await reader.ReadLineAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

    private static async Task WriteLineAsync(
        StreamWriter writer,
        string line,
        CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task StopListenerAsync(Task runTask, CancellationTokenSource cts)
    {
        await cts.CancelAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
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
        private readonly StoredMessage _message;

        public FakePop3MailboxStore(StoredMessage message)
        {
            _message = message;
        }

        public ValueTask<IReadOnlyList<Pop3MessageListing>> ListMessagesAsync(
            ImapAuthenticatedAccount account,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Pop3MessageListing> messages =
            [
                new Pop3MessageListing(_message.MessageId, _message.Uid, _message.Content.Length)
            ];
            return ValueTask.FromResult(messages);
        }

        public ValueTask<Stream> OpenMessageAsync(
            ImapAuthenticatedAccount account,
            long messageId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<Stream>(new MemoryStream(_message.Content, writable: false));

        public ValueTask DeleteMessagesAsync(
            ImapAuthenticatedAccount account,
            IReadOnlyCollection<long> messageIds,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    private sealed class FakeEventScriptExecutor : ISmtpEventScriptExecutor
    {
        private readonly Func<SmtpEventScriptExecutionRequest, SmtpRuleScriptExecutionResult> _execute;

        public FakeEventScriptExecutor(
            Func<SmtpEventScriptExecutionRequest, SmtpRuleScriptExecutionResult> execute)
        {
            _execute = execute;
        }

        public SmtpRuleScriptExecutionResult Execute(
            SmtpEventScriptExecutionRequest request,
            CancellationToken cancellationToken) =>
            _execute(request);
    }

    private sealed record StoredMessage(
        long MessageId,
        string Uid,
        byte[] Content);
}
