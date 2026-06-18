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

    private sealed class FakeClamAvDaemon : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly string _response;
        private readonly CancellationToken _cancellationToken;
        private readonly Task _serverTask;

        private FakeClamAvDaemon(
            TcpListener listener,
            string response,
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

        public List<byte[]> PayloadChunks { get; } = [];

        public static ValueTask<FakeClamAvDaemon> StartAsync(
            string response,
            CancellationToken cancellationToken)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ValueTask.FromResult(new FakeClamAvDaemon(listener, response, cancellationToken));
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
                Command = await ReadLineAsync(stream, _cancellationToken).ConfigureAwait(false);

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

                var responseBytes = Encoding.ASCII.GetBytes(_response);
                await stream.WriteAsync(responseBytes, _cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(_cancellationToken).ConfigureAwait(false);
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
