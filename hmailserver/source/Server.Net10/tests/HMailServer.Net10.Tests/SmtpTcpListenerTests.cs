using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Smtp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpTcpListenerTests
{
    [TestMethod]
    public async Task RunAsync_ReportsEachRunThroughTheLifecycleCallback()
    {
        using var firstCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = CreateListener(maxConcurrentConnections: 10);
        var firstRun = listener.RunAsync(firstCancellation.Token);
        await listener.Started.WaitAsync(firstCancellation.Token);
        await StopListenerAsync(firstRun, firstCancellation);

        using var secondCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var secondEndpoint = new TaskCompletionSource<IPEndPoint>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRun = listener.RunAsync(
            secondCancellation.Token,
            endpoint => secondEndpoint.TrySetResult(endpoint));

        var endpoint = await secondEndpoint.Task.WaitAsync(secondCancellation.Token);
        Assert.AreEqual(IPAddress.Loopback, endpoint.Address);
        await StopListenerAsync(secondRun, secondCancellation);
    }

    [TestMethod]
    public async Task RunAsync_AcceptsTcpClientAndDispatchesEhlo()
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

        Assert.AreEqual("220 hMailServer .NET 10 ESMTP ready", await ReadLineAsync(reader, cts.Token));
        await WriteLineAsync(writer, "EHLO client.example", cts.Token);

        Assert.AreEqual("250-mx.example.test", await ReadLineAsync(reader, cts.Token));
        Assert.AreEqual("250-SIZE 20971520", await ReadLineAsync(reader, cts.Token));
        Assert.AreEqual("250 HELP", await ReadLineAsync(reader, cts.Token));

        await WriteLineAsync(writer, "QUIT", cts.Token);
        Assert.AreEqual("221 mx.example.test closing connection", await ReadLineAsync(reader, cts.Token));

        await StopListenerAsync(runTask, cts);
    }

    [TestMethod]
    public async Task RunAsync_StagesFragmentedDataUntilTerminatorAndQueuesAfterReceiverRelease()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var receiver = new ControllableMessageReceiver();
        var listener = new SmtpTcpListener(
            new SmtpSession(
                new SmtpSessionOptions { ServerName = "mx.example.test" },
                receiver),
            new PlainSmtpConnectionStreamFactory(),
            new SmtpTcpListenerOptions
            {
                ListenAddress = IPAddress.Loopback,
                Port = 0,
                Backlog = 16,
                MaxConcurrentConnections = 1,
                ShutdownGracePeriod = TimeSpan.FromSeconds(1)
            });
        var runTask = listener.RunAsync(cts.Token);

        try
        {
            var endpoint = await listener.Started.WaitAsync(cts.Token);
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
            await using var stream = client.GetStream();
            using var reader = CreateReader(stream);
            await using var writer = CreateWriter(stream);

            Assert.AreEqual("220 hMailServer .NET 10 ESMTP ready", await ReadLineAsync(reader, cts.Token));
            await WriteLineAsync(writer, "EHLO client.example", cts.Token);
            Assert.AreEqual("250-mx.example.test", await ReadLineAsync(reader, cts.Token));
            Assert.AreEqual("250-SIZE 20971520", await ReadLineAsync(reader, cts.Token));
            Assert.AreEqual("250 HELP", await ReadLineAsync(reader, cts.Token));

            await WriteLineAsync(writer, "MAIL FROM:<sender@example.test>", cts.Token);
            Assert.AreEqual("250 OK", await ReadLineAsync(reader, cts.Token));
            await WriteLineAsync(writer, "RCPT TO:<recipient@example.test>", cts.Token);
            Assert.AreEqual("250 OK", await ReadLineAsync(reader, cts.Token));
            await WriteLineAsync(writer, "DATA", cts.Token);
            Assert.AreEqual("354 Start mail input; end with <CRLF>.<CRLF>", await ReadLineAsync(reader, cts.Token));

            await WriteFragmentAsync(writer, "Subject: Fragmented\r\n\r\nfirst\r\n", cts.Token);
            await WriteFragmentAsync(writer, "..dot-stuffed\r\nlast", cts.Token);
            await Task.Delay(100, cts.Token);

            Assert.AreEqual(0, receiver.InvocationCount);
            Assert.IsFalse(receiver.InvocationStarted.Task.IsCompleted);

            await WriteFragmentAsync(writer, "\r\n.\r\n", cts.Token);
            await receiver.InvocationStarted.Task.WaitAsync(cts.Token);

            Assert.IsNotNull(receiver.LastRequest);
            Assert.AreEqual("sender@example.test", receiver.LastRequest.MailFrom);
            CollectionAssert.AreEqual(
                new[] { "recipient@example.test" },
                receiver.LastRequest.Recipients.Select(static recipient => recipient.Address).ToArray());
            Assert.AreEqual(
                "Subject: Fragmented\r\n\r\nfirst\r\n.dot-stuffed\r\nlast\r\n",
                Encoding.Latin1.GetString(receiver.LastRequest.MessageData));

            receiver.Release();
            Assert.AreEqual("250 Queued", await ReadLineAsync(reader, cts.Token));

            await WriteLineAsync(writer, "QUIT", cts.Token);
            Assert.AreEqual("221 mx.example.test closing connection", await ReadLineAsync(reader, cts.Token));
        }
        finally
        {
            receiver.Release();
            await StopListenerAsync(runTask, cts);
        }
    }

    [TestMethod]
    public async Task RunAsync_PropagatesLoopbackAddressToSmtpAuthenticationBoundary()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var boundary = new CapturingClientAwareAuthenticationService(
            ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(77, "user@example.test")));
        var listener = CreateListener(
            maxConcurrentConnections: 10,
            clientAwareAuthenticationService: boundary);
        var runTask = listener.RunAsync(cts.Token);

        try
        {
            var endpoint = await listener.Started.WaitAsync(cts.Token);

            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
            await using var stream = client.GetStream();
            using var reader = CreateReader(stream);
            await using var writer = CreateWriter(stream);

            Assert.AreEqual("220 hMailServer .NET 10 ESMTP ready", await ReadLineAsync(reader, cts.Token));
            await WriteLineAsync(writer, "EHLO client.example", cts.Token);
            Assert.AreEqual("250-mx.example.test", await ReadLineAsync(reader, cts.Token));
            Assert.AreEqual("250-SIZE 20971520", await ReadLineAsync(reader, cts.Token));
            Assert.AreEqual("250-AUTH PLAIN LOGIN", await ReadLineAsync(reader, cts.Token));
            Assert.AreEqual("250 HELP", await ReadLineAsync(reader, cts.Token));

            var authToken = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(string.Concat('\0', "user@example.test", '\0', "secret")));
            await WriteLineAsync(writer, $"AUTH PLAIN {authToken}", cts.Token);

            Assert.AreEqual("235 Authentication successful", await ReadLineAsync(reader, cts.Token));
            Assert.IsNotNull(boundary.LastRequest);
            Assert.AreEqual(IPAddress.Loopback, boundary.LastRequest.ClientAddress);
            Assert.AreEqual(ClientAuthenticationCaller.Smtp, boundary.LastRequest.Caller);
            Assert.AreEqual("user@example.test", boundary.LastRequest.Username);
            Assert.AreEqual("secret", boundary.LastRequest.Password);

            await WriteLineAsync(writer, "QUIT", cts.Token);
            Assert.AreEqual("221 mx.example.test closing connection", await ReadLineAsync(reader, cts.Token));
        }
        finally
        {
            await StopListenerAsync(runTask, cts);
        }
    }

    [TestMethod]
    public async Task RunAsync_Replies421WhenConnectionLimitIsReached()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = CreateListener(maxConcurrentConnections: 1);
        var runTask = listener.RunAsync(cts.Token);
        var endpoint = await listener.Started.WaitAsync(cts.Token);

        using var firstClient = new TcpClient();
        await firstClient.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
        await using var firstStream = firstClient.GetStream();
        using var firstReader = CreateReader(firstStream);
        Assert.AreEqual("220 hMailServer .NET 10 ESMTP ready", await ReadLineAsync(firstReader, cts.Token));

        using var secondClient = new TcpClient();
        await secondClient.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
        await using var secondStream = secondClient.GetStream();
        using var secondReader = CreateReader(secondStream);
        Assert.AreEqual("421 Too many concurrent SMTP connections", await ReadLineAsync(secondReader, cts.Token));

        firstClient.Dispose();
        await StopListenerAsync(runTask, cts);
    }

    [TestMethod]
    public async Task RunAsync_RunsOnClientConnectBeforeGreetingAndCanReject()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        SmtpEventScriptExecutionRequest? capturedRequest = null;
        var callCount = 0;
        var listener = CreateListener(
            maxConcurrentConnections: 1,
            new FakeEventScriptExecutor(
                request =>
                {
                    callCount++;
                    capturedRequest = request;
                    return callCount == 1
                        ? SmtpRuleScriptExecutionResult.Failure("554 Rejected")
                        : SmtpRuleScriptExecutionResult.Continue(request.MessageData);
                }));
        var runTask = listener.RunAsync(cts.Token);
        var endpoint = await listener.Started.WaitAsync(cts.Token);

        using (var rejectedClient = new TcpClient())
        {
            await rejectedClient.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
            await using var rejectedStream = rejectedClient.GetStream();
            using var rejectedReader = CreateReader(rejectedStream);

            Assert.IsNull(await ReadLineAsync(rejectedReader, cts.Token));
        }

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("OnClientConnect", capturedRequest.EventName);
        Assert.AreEqual(SmtpEventScriptArgumentShape.ClientOnly, capturedRequest.ArgumentShape);
        Assert.IsFalse(string.IsNullOrWhiteSpace(capturedRequest.Client.IPAddress));
        Assert.IsTrue(capturedRequest.Client.Port > 0);
        Assert.IsTrue(capturedRequest.Client.SessionId > 0);

        using var acceptedClient = new TcpClient();
        await acceptedClient.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
        await using var acceptedStream = acceptedClient.GetStream();
        using var acceptedReader = CreateReader(acceptedStream);

        Assert.AreEqual("220 hMailServer .NET 10 ESMTP ready", await ReadLineAsync(acceptedReader, cts.Token));

        await StopListenerAsync(runTask, cts);
    }

    [TestMethod]
    [TestCategory("LiveProtocolAcceptance")]
    public async Task LoopbackConcurrency_AcceptsOneThousandClients()
    {
        const int clientCount = 1000;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var listener = new SmtpTcpListener(
            new SmtpSession(new SmtpSessionOptions { ServerName = "mx.example.test" }),
            new PlainSmtpConnectionStreamFactory(),
            new SmtpTcpListenerOptions
            {
                ListenAddress = IPAddress.Loopback,
                Port = 0,
                Backlog = 1024,
                MaxConcurrentConnections = clientCount + 32,
                ShutdownGracePeriod = TimeSpan.FromSeconds(1)
            });
        var runTask = listener.RunAsync(cts.Token);
        var endpoint = await listener.Started.WaitAsync(cts.Token);

        var banners = new ConcurrentQueue<string>();
        var tasks = Enumerable.Range(0, clientCount).Select(
            async _ =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
                await using var stream = client.GetStream();
                using var reader = CreateReader(stream);
                banners.Enqueue(await ReadLineAsync(reader, cts.Token) ?? string.Empty);
            }).ToArray();

        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            await StopListenerAsync(runTask, cts);
        }

        Assert.AreEqual(clientCount, banners.Count);
        Assert.IsTrue(banners.All(banner => banner == "220 hMailServer .NET 10 ESMTP ready"));
    }

    private static SmtpTcpListener CreateListener(
        int maxConcurrentConnections,
        ISmtpEventScriptExecutor? eventScriptExecutor = null,
        IClientAwareAuthenticationService? clientAwareAuthenticationService = null)
    {
        IImapAccountAuthenticator? accountAuthenticator = clientAwareAuthenticationService is null
            ? null
            : new FakeAccountAuthenticator();
        var session = new SmtpSession(
            new SmtpSessionOptions { ServerName = "mx.example.test" },
            accountAuthenticator: accountAuthenticator,
            clientAwareAuthenticationService: clientAwareAuthenticationService);
        return new SmtpTcpListener(
            session,
            new PlainSmtpConnectionStreamFactory(),
            new SmtpTcpListenerOptions
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

    private static async Task WriteFragmentAsync(
        StreamWriter writer,
        string fragment,
        CancellationToken cancellationToken)
    {
        await writer.WriteAsync(fragment.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class FakeEventScriptExecutor : ISmtpEventScriptExecutor
    {
        private readonly Func<SmtpEventScriptExecutionRequest, SmtpRuleScriptExecutionResult> _execute;

        public FakeEventScriptExecutor(Func<SmtpEventScriptExecutionRequest, SmtpRuleScriptExecutionResult> execute)
        {
            _execute = execute;
        }

        public SmtpRuleScriptExecutionResult Execute(
            SmtpEventScriptExecutionRequest request,
            CancellationToken cancellationToken) =>
            _execute(request);
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

    private sealed class ControllableMessageReceiver : ISmtpMessageReceiver
    {
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> InvocationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InvocationCount { get; private set; }

        public SmtpReceiveRequest? LastRequest { get; private set; }

        public async ValueTask<SmtpReceiveResult> ReceiveAsync(
            SmtpReceiveRequest request,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            LastRequest = request;
            InvocationStarted.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return SmtpReceiveResult.Success();
        }

        public void Release() => _release.TrySetResult(true);
    }
}
