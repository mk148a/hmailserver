using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ClamAvInstreamClientTests
{
    [TestMethod]
    public async Task ScanAsync_SendsInstreamFramesAndParsesCleanResponse()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var daemon = await FakeClamAvDaemon.StartAsync("stream: OK\r\n", timeout.Token);
        await using (daemon.ConfigureAwait(false))
        {
            var client = new ClamAvInstreamClient(
                new ClamAvInstreamClientOptions
                {
                    Host = IPAddress.Loopback.ToString(),
                    Port = daemon.Port,
                    Timeout = TimeSpan.FromSeconds(5),
                    ChunkSize = 3
                });

            var result = await client
                .ScanAsync("abcdef"u8.ToArray(), timeout.Token)
                .ConfigureAwait(false);

            Assert.IsTrue(result.Succeeded, result.Details);
            Assert.IsFalse(result.IsInfected);
            Assert.AreEqual("nINSTREAM", daemon.Command);
            CollectionAssert.AreEqual(
                new[] { "abc", "def" },
                daemon.PayloadChunks.Select(static chunk => Encoding.ASCII.GetString(chunk)).ToArray());
        }
    }

    [TestMethod]
    public void ParseResponse_ExtractsVirusName()
    {
        var result = ClamAvInstreamClient.ParseResponse("stream: Eicar-Test-Signature FOUND");

        Assert.IsTrue(result.Succeeded);
        Assert.IsTrue(result.IsInfected);
        Assert.AreEqual("Eicar-Test-Signature", result.VirusName);
    }

    [TestMethod]
    public async Task ScannerTestRuntime_ScansCleanAndEicarPayloads()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var daemon = await FakeClamAvDaemon.StartAsync(
            [
                "stream: OK\r\n",
                "stream: Eicar-Test-Signature FOUND\r\n"
            ],
            timeout.Token);
        await using (daemon.ConfigureAwait(false))
        {
            var runtime = new ClamAvScannerTestRuntime(
                new ClamAvInstreamClientOptions
                {
                    Host = IPAddress.Loopback.ToString(),
                    Port = daemon.Port,
                    Timeout = TimeSpan.FromSeconds(5),
                    ChunkSize = 1024
                });

            var result = runtime.TestConnection(IPAddress.Loopback.ToString(), daemon.Port);

            Assert.IsTrue(result.Succeeded, result.ResultText);
            Assert.AreEqual("stream: Eicar-Test-Signature FOUND", result.ResultText);
            CollectionAssert.AreEqual(
                new[] { "nINSTREAM", "nINSTREAM" },
                daemon.Commands.ToArray());
            Assert.AreEqual("Test", Encoding.ASCII.GetString(daemon.PayloadChunks[0]));
            StringAssert.Contains(
                Encoding.ASCII.GetString(daemon.PayloadChunks[1]),
                "EICAR-STANDARD-ANTIVIRUS-TEST-FILE");
        }
    }

    private sealed class FakeClamAvDaemon : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly IReadOnlyList<string> _responses;
        private readonly CancellationToken _cancellationToken;
        private readonly Task _serverTask;

        private FakeClamAvDaemon(
            TcpListener listener,
            IReadOnlyList<string> responses,
            CancellationToken cancellationToken)
        {
            _listener = listener;
            _responses = responses;
            _cancellationToken = cancellationToken;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _serverTask = RunAsync();
        }

        public int Port { get; }

        public string Command { get; private set; } = string.Empty;

        public List<string> Commands { get; } = [];

        public List<byte[]> PayloadChunks { get; } = [];

        public static ValueTask<FakeClamAvDaemon> StartAsync(
            string response,
            CancellationToken cancellationToken)
        {
            return StartAsync([response], cancellationToken);
        }

        public static ValueTask<FakeClamAvDaemon> StartAsync(
            IReadOnlyList<string> responses,
            CancellationToken cancellationToken)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ValueTask.FromResult(new FakeClamAvDaemon(listener, responses, cancellationToken));
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
                foreach (var response in _responses)
                {
                    using var client = await _listener.AcceptTcpClientAsync(_cancellationToken).ConfigureAwait(false);
                    await using var stream = client.GetStream();
                    var command = await ReadLineAsync(stream, _cancellationToken).ConfigureAwait(false);
                    if (Command.Length == 0)
                    {
                        Command = command;
                    }

                    Commands.Add(command);

                    while (true)
                    {
                        var lengthPrefix = await ReadExactAsync(stream, 4, _cancellationToken).ConfigureAwait(false);
                        var length = BinaryPrimitives.ReadInt32BigEndian(lengthPrefix);
                        if (length == 0)
                        {
                            break;
                        }

                        PayloadChunks.Add(await ReadExactAsync(stream, length, _cancellationToken).ConfigureAwait(false));
                    }

                    var responseBytes = Encoding.ASCII.GetBytes(response);
                    await stream.WriteAsync(responseBytes, _cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(_cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }

        private static async ValueTask<string> ReadLineAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            using var output = new MemoryStream();
            var buffer = new byte[1];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new IOException("Client disconnected.");
                }

                if (buffer[0] == (byte)'\n')
                {
                    break;
                }

                output.WriteByte(buffer[0]);
            }

            return Encoding.ASCII.GetString(output.ToArray()).TrimEnd('\r');
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
