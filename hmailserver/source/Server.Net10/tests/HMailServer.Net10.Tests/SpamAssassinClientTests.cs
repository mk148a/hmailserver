using System.Net;
using System.Net.Sockets;
using System.Text;
using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SpamAssassinClientTests
{
    [TestMethod]
    public async Task ProcessAsync_SendsProcessRequestAndReturnsProcessedMessage()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var processedMessage =
            "Return-Path: <sender@example.test>\r\n" +
            "X-Spam-Status: No, score=0.1 required=5.0\r\n" +
            "Subject: checked\r\n" +
            "\r\n" +
            "Body\r\n";
        var daemon = await FakeSpamAssassinDaemon.StartAsync(
            CreateResponse(processedMessage),
            timeout.Token);
        await using (daemon.ConfigureAwait(false))
        {
            var client = CreateClient(daemon.Port);

            var result = await client
                .ProcessAsync(
                    "Subject: original\r\n\r\nBody\r\n"u8.ToArray(),
                    "sender@example.test",
                    timeout.Token)
                .ConfigureAwait(false);

            Assert.IsTrue(result.Succeeded, result.Details);
            Assert.IsFalse(result.IsSpam);
            Assert.AreEqual("PROCESS SPAMC/1.2", daemon.Command);
            StringAssert.StartsWith(daemon.PayloadText, "Return-Path: <sender@example.test>\r\n");
            Assert.AreEqual(Encoding.ASCII.GetByteCount(daemon.PayloadText), daemon.DeclaredContentLength);
            StringAssert.Contains(Encoding.Latin1.GetString(result.MessageData), "X-Spam-Status: No");
            Assert.IsFalse(Encoding.Latin1.GetString(result.MessageData).StartsWith("Return-Path:", StringComparison.OrdinalIgnoreCase));
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ClassifiesSpamAndParsesIntegerScore()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var processedMessage =
            "X-Spam-Status: Yes, score=7.8 required=5.0 tests=LOCAL_TEST\r\n" +
            "Subject: spam\r\n" +
            "\r\n" +
            "Body\r\n";
        var daemon = await FakeSpamAssassinDaemon.StartAsync(
            CreateResponse(processedMessage),
            timeout.Token);
        await using (daemon.ConfigureAwait(false))
        {
            var client = CreateClient(daemon.Port);

            var result = await client
                .ProcessAsync("Subject: spam\r\n\r\nBody\r\n"u8.ToArray(), "", timeout.Token)
                .ConfigureAwait(false);

            Assert.IsTrue(result.Succeeded, result.Details);
            Assert.IsTrue(result.IsSpam);
            Assert.AreEqual(7, result.Score);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_PreservesOriginalMessageWhenHeaderIsInvalid()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var originalMessage = "Subject: original\r\n\r\nBody\r\n"u8.ToArray();
        var daemon = await FakeSpamAssassinDaemon.StartAsync(
            "SPAMD/1.1 0 EX_OK\r\nContent-length: -1\r\n\r\n"u8.ToArray(),
            timeout.Token);
        await using (daemon.ConfigureAwait(false))
        {
            var client = CreateClient(daemon.Port);

            var result = await client
                .ProcessAsync(originalMessage, "", timeout.Token)
                .ConfigureAwait(false);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.AreEqual(originalMessage, result.MessageData);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_PreservesOriginalMessageWhenBodyIsPartial()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var originalMessage = "Subject: original\r\n\r\nBody\r\n"u8.ToArray();
        var daemon = await FakeSpamAssassinDaemon.StartAsync(
            "SPAMD/1.1 0 EX_OK\r\nContent-length: 42\r\n\r\nshort"u8.ToArray(),
            timeout.Token);
        await using (daemon.ConfigureAwait(false))
        {
            var client = CreateClient(daemon.Port);

            var result = await client
                .ProcessAsync(originalMessage, "", timeout.Token)
                .ConfigureAwait(false);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.AreEqual(originalMessage, result.MessageData);
        }
    }

    [TestMethod]
    public async Task ConnectionTestRuntime_SendsLegacyGtubeMessageAndReturnsProcessedMessage()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var processedMessage =
            "X-Spam-Status: Yes, score=999.0 required=5.0 tests=GTUBE\r\n" +
            "Subject: SpamAssassin test\r\n" +
            "\r\n" +
            "GTUBE detected\r\n";
        var daemon = await FakeSpamAssassinDaemon.StartAsync(
            CreateResponse(processedMessage),
            timeout.Token);
        await using (daemon.ConfigureAwait(false))
        {
            var runtime = new SpamAssassinConnectionTestRuntime(
                new SpamAssassinClientOptions
                {
                    Host = IPAddress.Loopback.ToString(),
                    Port = daemon.Port,
                    Timeout = TimeSpan.FromSeconds(5),
                    MaxResponseBytes = 1024 * 1024
                });

            var result = runtime.TestConnection(IPAddress.Loopback.ToString(), daemon.Port);

            Assert.IsTrue(result.Succeeded, result.ResultText);
            StringAssert.Contains(result.ResultText, "X-Spam-Status: Yes");
            Assert.AreEqual("PROCESS SPAMC/1.2", daemon.Command);
            StringAssert.Contains(daemon.PayloadText, "From: SpamAssassinTest@example.com\r\n");
            StringAssert.Contains(
                daemon.PayloadText,
                "XJS*C4JDBQADN1.NSBN3*2IDNEN*GTUBE-STANDARD-ANTI-UBE-TEST-EMAIL*C.34X.");
        }
    }

    private static SpamAssassinClient CreateClient(int port) =>
        new(
            new SpamAssassinClientOptions
            {
                Host = IPAddress.Loopback.ToString(),
                Port = port,
                Timeout = TimeSpan.FromSeconds(5),
                MaxResponseBytes = 1024 * 1024
            });

    private static byte[] CreateResponse(string processedMessage)
    {
        var body = Encoding.Latin1.GetBytes(processedMessage);
        return Encoding.ASCII.GetBytes(
                "SPAMD/1.1 0 EX_OK\r\nContent-length: " +
                body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "\r\n\r\n")
            .Concat(body)
            .ToArray();
    }

    private sealed class FakeSpamAssassinDaemon : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly byte[] _response;
        private readonly CancellationToken _cancellationToken;
        private readonly Task _serverTask;

        private FakeSpamAssassinDaemon(
            TcpListener listener,
            byte[] response,
            CancellationToken cancellationToken)
        {
            _listener = listener;
            _response = response;
            _cancellationToken = cancellationToken;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _serverTask = RunAsync();
        }

        public int Port { get; }

        public string Command { get; private set; } = string.Empty;

        public int DeclaredContentLength { get; private set; }

        public string PayloadText { get; private set; } = string.Empty;

        public static ValueTask<FakeSpamAssassinDaemon> StartAsync(
            byte[] response,
            CancellationToken cancellationToken)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ValueTask.FromResult(new FakeSpamAssassinDaemon(listener, response, cancellationToken));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _serverTask.ConfigureAwait(false);
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_cancellationToken).ConfigureAwait(false);
                await using var stream = client.GetStream();
                var requestHeader = await ReadHeaderAsync(stream, _cancellationToken).ConfigureAwait(false);
                var requestLines = requestHeader.Split("\r\n", StringSplitOptions.None);
                Command = requestLines[0];
                DeclaredContentLength = ReadContentLength(requestLines);
                var payload = await ReadExactAsync(stream, DeclaredContentLength, _cancellationToken).ConfigureAwait(false);
                PayloadText = Encoding.ASCII.GetString(payload);

                await stream.WriteAsync(_response, _cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(_cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }

        private static int ReadContentLength(string[] requestLines)
        {
            const string prefix = "Content-length:";
            foreach (var line in requestLines.Skip(1))
            {
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return int.Parse(
                        line[prefix.Length..].Trim(),
                        System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            throw new InvalidDataException("Missing Content-length.");
        }

        private static async ValueTask<string> ReadHeaderAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            using var output = new MemoryStream();
            var buffer = new byte[1];
            var previous = new Queue<byte>(4);
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new IOException("Client disconnected.");
                }

                output.WriteByte(buffer[0]);
                previous.Enqueue(buffer[0]);
                if (previous.Count > 4)
                {
                    previous.Dequeue();
                }

                if (previous.SequenceEqual("\r\n\r\n"u8.ToArray()))
                {
                    break;
                }
            }

            var bytes = output.ToArray();
            return Encoding.ASCII.GetString(bytes, 0, bytes.Length - 4);
        }

        private static async ValueTask<byte[]> ReadExactAsync(
            Stream stream,
            int length,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new IOException("Client disconnected.");
                }

                offset += read;
            }

            return buffer;
        }
    }
}
