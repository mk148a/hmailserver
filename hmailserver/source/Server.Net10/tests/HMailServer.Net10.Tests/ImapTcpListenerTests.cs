using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapTcpListenerTests
{
    [TestMethod]
    public async Task RunAsync_AcceptsTcpClientAndDispatchesUidSearch()
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

        Assert.AreEqual("* OK hMailServer .NET 10 IMAP ready", await ReadLineAsync(reader, cts.Token));
        await WriteLineAsync(writer, "A001 UID SEARCH TEXT \"invoice\" UNSEEN", cts.Token);

        Assert.AreEqual("* SEARCH 101 105", await ReadLineAsync(reader, cts.Token));
        Assert.AreEqual("A001 OK SEARCH completed", await ReadLineAsync(reader, cts.Token));

        await WriteLineAsync(writer, "A002 LOGOUT", cts.Token);
        Assert.AreEqual("* BYE hMailServer IMAP session closing", await ReadLineAsync(reader, cts.Token));
        Assert.AreEqual("A002 OK LOGOUT completed", await ReadLineAsync(reader, cts.Token));

        await StopListenerAsync(runTask, cts);
    }

    [TestMethod]
    public async Task RunAsync_PropagatesLoopbackAddressToImapAuthenticationBoundary()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var boundary = new CapturingClientAwareAuthenticationService(
            ImapAuthenticationResult.Success(new ImapAuthenticatedAccount(77, "user@example.test")));
        var listener = CreateListener(
            maxConcurrentConnections: 10,
            clientAwareAuthenticationService: boundary,
            sessionContext: new ImapSessionContext());
        var runTask = listener.RunAsync(cts.Token);

        try
        {
            var endpoint = await listener.Started.WaitAsync(cts.Token);

            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
            await using var stream = client.GetStream();
            using var reader = CreateReader(stream);
            await using var writer = CreateWriter(stream);

            Assert.AreEqual("* OK hMailServer .NET 10 IMAP ready", await ReadLineAsync(reader, cts.Token));
            await WriteLineAsync(writer, "A001 LOGIN user@example.test secret", cts.Token);

            Assert.AreEqual("A001 OK LOGIN completed", await ReadLineAsync(reader, cts.Token));
            Assert.IsNotNull(boundary.LastRequest);
            Assert.AreEqual(IPAddress.Loopback, boundary.LastRequest.ClientAddress);
            Assert.AreEqual(ClientAuthenticationCaller.Imap, boundary.LastRequest.Caller);
            Assert.AreEqual("user@example.test", boundary.LastRequest.Username);
            Assert.AreEqual("secret", boundary.LastRequest.Password);

            await WriteLineAsync(writer, "A002 LOGOUT", cts.Token);
            Assert.AreEqual("* BYE hMailServer IMAP session closing", await ReadLineAsync(reader, cts.Token));
            Assert.AreEqual("A002 OK LOGOUT completed", await ReadLineAsync(reader, cts.Token));
        }
        finally
        {
            await StopListenerAsync(runTask, cts);
        }
    }

    [TestMethod]
    public async Task RunAsync_RepliesByeWhenConnectionLimitIsReached()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = CreateListener(maxConcurrentConnections: 1);
        var runTask = listener.RunAsync(cts.Token);
        var endpoint = await listener.Started.WaitAsync(cts.Token);

        using var firstClient = new TcpClient();
        await firstClient.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
        await using var firstStream = firstClient.GetStream();
        using var firstReader = CreateReader(firstStream);
        Assert.AreEqual("* OK hMailServer .NET 10 IMAP ready", await ReadLineAsync(firstReader, cts.Token));

        using var secondClient = new TcpClient();
        await secondClient.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
        await using var secondStream = secondClient.GetStream();
        using var secondReader = CreateReader(secondStream);
        Assert.AreEqual("* BYE Too many concurrent IMAP connections", await ReadLineAsync(secondReader, cts.Token));

        firstClient.Dispose();
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


    [TestMethod]
    [TestCategory("LiveProtocolAcceptance")]
    public async Task LoopbackAcceptLatency_ServesBannerWithinBudget()
    {
        const int clientCount = 200;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var listener = CreateListener(maxConcurrentConnections: 64);
        var runTask = listener.RunAsync(cts.Token);
        var endpoint = await listener.Started.WaitAsync(cts.Token);

        var latencies = new List<double>(clientCount);
        try
        {
            for (var i = 0; i < clientCount; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                using var client = new TcpClient();
                await client.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
                await using var stream = client.GetStream();
                using var reader = CreateReader(stream);
                var banner = await ReadLineAsync(reader, cts.Token);
                stopwatch.Stop();
                Assert.AreEqual("* OK hMailServer .NET 10 IMAP ready", banner);
                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            latencies.Sort();
            var p95 = Percentile(latencies, 0.95);
            Assert.AreEqual(clientCount, latencies.Count);
            Assert.IsTrue(p95 < TimeSpan.FromSeconds(5).TotalMilliseconds, "IMAP" + " accept p95 (" + p95.ToString("0.###") + "ms) exceeded budget.");
        }
        finally
        {
            await StopListenerAsync(runTask, cts);
        }
    }

    private static double Percentile(IReadOnlyList<double> orderedValues, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * orderedValues.Count) - 1;
        return orderedValues[Math.Clamp(index, 0, orderedValues.Count - 1)];
    }

    private static ImapTcpListener CreateListener(
        int maxConcurrentConnections,
        ISmtpEventScriptExecutor? eventScriptExecutor = null,
        IClientAwareAuthenticationService? clientAwareAuthenticationService = null,
        ImapSessionContext? sessionContext = null)
    {
        var searchIndex = new FakeSearchIndex(
        [
            new MessageIdentity(1, 10, 20, 101),
            new MessageIdentity(2, 10, 20, 105)
        ]);
        var executor = new ImapSearchExecutor(searchIndex);
        var handler = new ImapSearchCommandHandler(new ImapSearchCommandParser(), executor);
        IImapAccountAuthenticator? accountAuthenticator = clientAwareAuthenticationService is null
            ? null
            : new FakeAuthenticator();
        var session = new ImapSession(
            handler,
            accountAuthenticator: accountAuthenticator,
            clientAwareAuthenticationService: clientAwareAuthenticationService);
        return new ImapTcpListener(
            session,
            new FixedImapSessionContextProvider(sessionContext ?? new ImapSessionContext(10, 20)),
            new PlainImapConnectionStreamFactory(),
            new ImapTcpListenerOptions
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

    private sealed class FakeSearchIndex : IMessageSearchIndex
    {
        private readonly IReadOnlyList<MessageIdentity> _matches;

        public FakeSearchIndex(IReadOnlyList<MessageIdentity> matches)
        {
            _matches = matches;
        }

        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public ValueTask QueueForIndexingAsync(MessageIdentity identity, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask UpsertAsync(MessageSearchDocument document, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async IAsyncEnumerable<MessageIdentity> SearchAsync(
            ImapSearchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            foreach (var match in _matches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return match;
            }
        }
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

    private sealed class FakeAuthenticator : IImapAccountAuthenticator
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
}
