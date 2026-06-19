using System.Net;
using System.Net.Sockets;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Pop3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class TcpExternalFetchSessionFactoryTests
{
    [TestMethod]
    public async Task ConnectAsync_ListsDownloadsDeletesAndQuitsPop3Session()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(listener, commands, timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory();
            await using var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port), timeout.Token)
                .ConfigureAwait(false);

            var messages = await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(1, messages[0].SequenceNumber);
            Assert.AreEqual("uid-1", messages[0].Uid);

            var messageData = await session.DownloadMessageAsync(messages[0], timeout.Token).ConfigureAwait(false);
            CollectionAssert.AreEqual(
                Encoding.ASCII.GetBytes("Subject: fetched\r\n.dot-stuffed\r\n"),
                messageData);

            await session.DeleteMessageAsync(messages[0], timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);

        CollectionAssert.AreEqual(
            new[]
            {
                "USER external-user",
                "PASS external-password",
                "UIDL",
                "RETR 1",
                "DELE 1",
                "QUIT"
            },
            commands);
    }

    [TestMethod]
    public async Task ConnectAsync_StartTlsOptionalUsesPlaintextWhenCapaDoesNotAdvertiseStls()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerWithoutStlsCapabilityAsync(
            listener,
            commands,
            stopAfterCapa: false,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory();
            await using var session = await factory
                .ConnectAsync(
                    CreateAccount(endpoint.Port, ExternalFetchConnectionSecurity.StartTlsOptional),
                    timeout.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);

        CollectionAssert.AreEqual(
            new[]
            {
                "CAPA",
                "USER external-user",
                "PASS external-password",
                "QUIT"
            },
            commands);
    }

    [TestMethod]
    public async Task ConnectAsync_StartTlsRequiredFailsWhenCapaDoesNotAdvertiseStls()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerWithoutStlsCapabilityAsync(
            listener,
            commands,
            stopAfterCapa: true,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await factory
                    .ConnectAsync(
                        CreateAccount(endpoint.Port, ExternalFetchConnectionSecurity.StartTlsRequired),
                        timeout.Token)
                    .ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);

        CollectionAssert.AreEqual(
            new[] { "CAPA" },
            commands);
    }

    private static async Task RunPop3ServerAsync(
        TcpListener listener,
        List<string> commands,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        await WriteRawAsync(stream, "+OK fake server ready\r\n", cancellationToken).ConfigureAwait(false);

        while (true)
        {
            var command = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
            commands.Add(command);
            if (command.StartsWith("USER ", StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith("PASS ", StringComparison.OrdinalIgnoreCase))
            {
                await WriteRawAsync(stream, "+OK\r\n", cancellationToken).ConfigureAwait(false);
            }
            else if (command.Equals("UIDL", StringComparison.OrdinalIgnoreCase))
            {
                await WriteRawAsync(stream, "+OK\r\n1 uid-1\r\n.\r\n", cancellationToken).ConfigureAwait(false);
            }
            else if (command.Equals("RETR 1", StringComparison.OrdinalIgnoreCase))
            {
                await WriteRawAsync(stream, "+OK\r\nSubject: fetched\r\n..dot-stuffed\r\n.\r\n", cancellationToken).ConfigureAwait(false);
            }
            else if (command.Equals("DELE 1", StringComparison.OrdinalIgnoreCase))
            {
                await WriteRawAsync(stream, "+OK\r\n", cancellationToken).ConfigureAwait(false);
            }
            else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await WriteRawAsync(stream, "+OK bye\r\n", cancellationToken).ConfigureAwait(false);
                break;
            }
            else
            {
                await WriteRawAsync(stream, "-ERR unexpected\r\n", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task RunPop3ServerWithoutStlsCapabilityAsync(
        TcpListener listener,
        List<string> commands,
        bool stopAfterCapa,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        await WriteRawAsync(stream, "+OK fake server ready\r\n", cancellationToken).ConfigureAwait(false);

        while (true)
        {
            var command = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
            commands.Add(command);
            if (command.Equals("CAPA", StringComparison.OrdinalIgnoreCase))
            {
                await WriteRawAsync(stream, "+OK capability list follows\r\nUIDL\r\nUSER\r\n.\r\n", cancellationToken).ConfigureAwait(false);
                if (stopAfterCapa)
                {
                    break;
                }
            }
            else if (command.StartsWith("USER ", StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith("PASS ", StringComparison.OrdinalIgnoreCase))
            {
                await WriteRawAsync(stream, "+OK\r\n", cancellationToken).ConfigureAwait(false);
            }
            else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await WriteRawAsync(stream, "+OK bye\r\n", cancellationToken).ConfigureAwait(false);
                break;
            }
            else
            {
                await WriteRawAsync(stream, "-ERR unexpected\r\n", cancellationToken).ConfigureAwait(false);
            }
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

        var value = Encoding.ASCII.GetString(output.ToArray());
        return value.EndsWith('\r')
            ? value[..^1]
            : value;
    }

    private static async ValueTask WriteRawAsync(
        Stream stream,
        string value,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ExternalFetchAccountLease CreateAccount(
        int port,
        ExternalFetchConnectionSecurity connectionSecurity = ExternalFetchConnectionSecurity.None) =>
        new(
            FetchAccountId: 77,
            AccountId: 42,
            Name: "External POP3",
            ServerAddress: IPAddress.Loopback.ToString(),
            ServerPort: port,
            ServerType: ExternalFetchServerType.Pop3,
            Username: "external-user",
            Password: "external-password",
            MinutesBetweenFetch: 10,
            DaysToKeep: 7,
            ProcessMimeRecipients: true,
            ProcessMimeDate: true,
            ConnectionSecurity: connectionSecurity,
            UseAntiSpam: true,
            UseAntiVirus: true,
            EnableRouteRecipients: false,
            MimeRecipientHeaders: "To,CC",
            AccountAddress: "user@example.test");
}
