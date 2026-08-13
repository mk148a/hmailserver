using System.Diagnostics;
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
    public void Options_DefaultOperationTimeoutMatchesLegacyLowLoadTimeout()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(900), new ExternalFetchPop3ClientOptions().OperationTimeout);
    }

    [TestMethod]
    public void Options_DefaultEgressPolicyIsEnforced()
    {
        Assert.IsTrue(new ExternalFetchPop3ClientOptions().EnforceEgressPolicy);
    }

    [TestMethod]
    public async Task ConnectAsync_ResolvesOnceAndPinsTheSelectedEndpoint()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var resolver = new RecordingAddressResolver([IPAddress.Loopback]);
        ExternalFetchEndpointDecision? decision = null;
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(
                new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false },
                addressResolver: resolver,
                endpointDecisionObserver: value => decision = value);
            await using var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port), timeout.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);

        Assert.AreEqual(1, resolver.CallCount);
        Assert.AreEqual(IPAddress.Loopback.ToString(), resolver.LastHostName);
        Assert.IsNotNull(decision);
        Assert.IsFalse(decision!.IsAllowed);
    }

    [TestMethod]
    public async Task ConnectAsync_PassesOriginalHostnameToTlsUpgrade()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var resolver = new RecordingAddressResolver([IPAddress.Loopback]);
        string? tlsTargetHost = null;
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(
                new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false },
                addressResolver: resolver,
                tlsStreamFactory: (stream, targetHost, _) =>
                {
                    tlsTargetHost = targetHost;
                    return ValueTask.FromResult<Stream>(stream);
                });
            var account = CreateAccount(endpoint.Port, ExternalFetchConnectionSecurity.Ssl) with
            {
                ServerAddress = "pop3.example.test"
            };

            await using var session = await factory
                .ConnectAsync(account, timeout.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        Assert.AreEqual("pop3.example.test", tlsTargetHost);
        Assert.AreEqual("pop3.example.test", resolver.LastHostName);
    }

    [TestMethod]
    public async Task ConnectAsync_EnforcedEgressPolicyRejectsPrivateAddressBeforeConnect()
    {
        var resolver = new RecordingAddressResolver([IPAddress.Parse("10.20.30.40")]);
        var factory = new TcpExternalFetchSessionFactory(
            new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = true },
            resolver);
        var account = CreateAccount(1) with { ServerAddress = "pop3.internal.test" };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await factory.ConnectAsync(account, CancellationToken.None).ConfigureAwait(false));

        Assert.AreEqual(1, resolver.CallCount);
        Assert.AreEqual("pop3.internal.test", resolver.LastHostName);
    }

    [TestMethod]
    public async Task ConnectAsync_DefaultOptionsRejectLoopbackBeforeConnect()
    {
        var resolver = new RecordingAddressResolver([IPAddress.Loopback]);
        var factory = new TcpExternalFetchSessionFactory(addressResolver: resolver);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await factory.ConnectAsync(CreateAccount(1), CancellationToken.None).ConfigureAwait(false));

        Assert.AreEqual(1, resolver.CallCount);
    }

    [TestMethod]
    public async Task ConnectAsync_StalledAddressResolutionExpiresAsTimeoutFailure()
    {
        using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var factory = new TcpExternalFetchSessionFactory(
            new ExternalFetchPop3ClientOptions { OperationTimeout = TimeSpan.FromMilliseconds(100), EnforceEgressPolicy = false },
            new StallingAddressResolver());

        await Assert.ThrowsExactlyAsync<TimeoutException>(
            async () => await factory.ConnectAsync(CreateAccount(110), testTimeout.Token).ConfigureAwait(false));
    }

    [TestMethod]
    public async Task ConnectAsync_StalledImplicitTlsHandshakeExpiresAsTimeoutFailure()
    {
        using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = RunControlServerAsync(
            listener,
            [],
            stallAfterGreeting: true,
            stallAfterUser: false,
            stallAfterQuit: false,
            testTimeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(
                new ExternalFetchPop3ClientOptions { OperationTimeout = TimeSpan.FromMilliseconds(100), EnforceEgressPolicy = false },
                tlsStreamFactory: async (stream, _, cancellationToken) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    return stream;
                });

            await Assert.ThrowsExactlyAsync<TimeoutException>(
                async () => await factory
                    .ConnectAsync(CreateAccount(endpoint.Port, ExternalFetchConnectionSecurity.Ssl), testTimeout.Token)
                    .ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ConnectAsync_StalledGreetingExpiresAsTimeoutFailure()
    {
        using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = RunControlServerAsync(
            listener,
            [],
            stallAfterGreeting: true,
            stallAfterUser: false,
            stallAfterQuit: false,
            testTimeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(
                new ExternalFetchPop3ClientOptions { OperationTimeout = TimeSpan.FromMilliseconds(100), EnforceEgressPolicy = false });

            await Assert.ThrowsExactlyAsync<TimeoutException>(
                async () => await factory.ConnectAsync(CreateAccount(endpoint.Port), testTimeout.Token).ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ConnectAsync_StalledControlReadExpiresAsTimeoutFailure()
    {
        using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = RunControlServerAsync(
            listener,
            "+OK ready\r\n"u8.ToArray(),
            stallAfterGreeting: false,
            stallAfterUser: true,
            stallAfterQuit: false,
            testTimeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(
                new ExternalFetchPop3ClientOptions { OperationTimeout = TimeSpan.FromMilliseconds(100), EnforceEgressPolicy = false });

            await Assert.ThrowsExactlyAsync<TimeoutException>(
                async () => await factory.ConnectAsync(CreateAccount(endpoint.Port), testTimeout.Token).ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ConnectAsync_AcceptsControlLineEndingAt250000ByteBudget()
    {
        using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = RunControlServerAsync(
            listener,
            CreateOkControlLine(250_000),
            stallAfterGreeting: false,
            stallAfterUser: false,
            stallAfterQuit: false,
            testTimeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(
                new ExternalFetchPop3ClientOptions { OperationTimeout = TimeSpan.FromSeconds(20), EnforceEgressPolicy = false });
            await using var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port), testTimeout.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ConnectAsync_RejectsControlLineExceeding250000ByteBudget()
    {
        using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = RunControlServerAsync(
            listener,
            CreateOkControlLine(250_001),
            stallAfterGreeting: true,
            stallAfterUser: false,
            stallAfterQuit: false,
            testTimeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(
                new ExternalFetchPop3ClientOptions { OperationTimeout = TimeSpan.FromSeconds(20), EnforceEgressPolicy = false });

            await Assert.ThrowsExactlyAsync<IOException>(
                async () => await factory.ConnectAsync(CreateAccount(endpoint.Port), testTimeout.Token).ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
    }

    [TestMethod]
    public async Task DisposeAsync_StalledQuitResponseIsBounded()
    {
        using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = RunControlServerAsync(
            listener,
            "+OK ready\r\n"u8.ToArray(),
            stallAfterGreeting: false,
            stallAfterUser: false,
            stallAfterQuit: true,
            testTimeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(
                new ExternalFetchPop3ClientOptions { OperationTimeout = TimeSpan.FromMilliseconds(100), EnforceEgressPolicy = false });
            var session = await factory.ConnectAsync(CreateAccount(endpoint.Port), testTimeout.Token).ConfigureAwait(false);
            var stopwatch = Stopwatch.StartNew();

            await session.DisposeAsync().ConfigureAwait(false);

            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"QUIT disposal took {stopwatch.Elapsed}.");
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ConnectAsync_CallerCancellationRemainsOperationCanceledException()
    {
        using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var callerCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = RunControlServerAsync(
            listener,
            [],
            stallAfterGreeting: true,
            stallAfterUser: false,
            stallAfterQuit: false,
            testTimeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(
                new ExternalFetchPop3ClientOptions { OperationTimeout = TimeSpan.FromSeconds(5), EnforceEgressPolicy = false });

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                async () => await factory.ConnectAsync(CreateAccount(endpoint.Port), callerCancellation.Token).ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ConnectAsync_ListsDownloadsDeletesAndQuitsPop3Session()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
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
    public async Task DownloadMessageAsync_ReturnsEmptyPayloadForEmptyRetrBody()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token,
            emptyRetrBody: true);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port), timeout.Token)
                .ConfigureAwait(false);

            var messages = await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false);
            var messageData = await session.DownloadMessageAsync(messages.Single(), timeout.Token).ConfigureAwait(false);

            Assert.AreEqual(0, messageData.Length);
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
            rejectCapa: false,
            stopAfterCapa: false,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
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
            rejectCapa: false,
            stopAfterCapa: true,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
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

    [TestMethod]
    public async Task ConnectAsync_StartTlsOptionalUsesPlaintextWhenCapaIsRejected()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerWithoutStlsCapabilityAsync(
            listener,
            commands,
            rejectCapa: true,
            stopAfterCapa: false,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
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
    public async Task ConnectAsync_StartTlsRequiredFailsWhenCapaIsRejected()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerWithoutStlsCapabilityAsync(
            listener,
            commands,
            rejectCapa: true,
            stopAfterCapa: true,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
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

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsRequired)]
    public async Task ConnectAsync_StartTlsModesFailWhenAdvertisedStlsIsRejected(
        ExternalFetchConnectionSecurity connectionSecurity)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerRejectingStlsAsync(listener, commands, timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await factory
                    .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                    .ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);

        CollectionAssert.AreEqual(
            new[] { "CAPA", "STLS" },
            commands);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsRequired)]
    public async Task ConnectAsync_RejectedGreetingFailsBeforeSendingCommands(
        ExternalFetchConnectionSecurity connectionSecurity)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = RunPop3ServerRejectingGreetingAsync(listener, timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await factory
                    .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                    .ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }

        var receivedBytes = await serverTask.ConfigureAwait(false);
        Assert.AreEqual(0, receivedBytes.Length);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task ConnectAsync_RejectedUserFailsBeforeSendingPassword(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerRejectingAuthenticationAsync(
            listener,
            commands,
            expectCapa,
            rejectUser: true,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await factory
                    .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                    .ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }

        var receivedAfterUser = await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user" }
                : new[] { "USER external-user" },
            commands);
        Assert.AreEqual(0, receivedAfterUser.Length);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task ConnectAsync_RejectedPasswordFailsBeforeListingMessages(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerRejectingAuthenticationAsync(
            listener,
            commands,
            expectCapa,
            rejectUser: false,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await factory
                    .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                    .ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }

        var receivedAfterPassword = await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password" }
                : new[] { "USER external-user", "PASS external-password" },
            commands);
        Assert.AreEqual(0, receivedAfterPassword.Length);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task ListMessagesAsync_RejectedUidlQuitsWithoutMessageCommands(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerWithoutStlsCapabilityAsync(
            listener,
            commands,
            rejectCapa: false,
            stopAfterCapa: false,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using (var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                .ConfigureAwait(false))
            {
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    async () => await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false));
            }
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password", "UIDL", "QUIT" }
                : new[] { "USER external-user", "PASS external-password", "UIDL", "QUIT" },
            commands);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task ListMessagesAsync_DisconnectBeforeUidlTerminatorRemainsFatal(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token,
            disconnectDuringUidlListing: true);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using (var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                .ConfigureAwait(false))
            {
                await Assert.ThrowsExactlyAsync<IOException>(
                    async () => await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false));
            }
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password", "UIDL" }
                : new[] { "USER external-user", "PASS external-password", "UIDL" },
            commands);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task ListMessagesAsync_SkipsMalformedUidlLinesAndKeepsValidRows(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token,
            uidlResponse: "+OK\r\nmissing-sequence\r\nx uid-invalid-sequence\r\n2\r\n3 \r\n1 uid-1\r\n4 uid-4\r\n.\r\n");
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using (var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                .ConfigureAwait(false))
            {
                var messages = await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false);

                Assert.AreEqual(2, messages.Count);
                Assert.AreEqual(1, messages[0].SequenceNumber);
                Assert.AreEqual("uid-1", messages[0].Uid);
                Assert.AreEqual(4, messages[1].SequenceNumber);
                Assert.AreEqual("uid-4", messages[1].Uid);
            }
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password", "UIDL", "QUIT" }
                : new[] { "USER external-user", "PASS external-password", "UIDL", "QUIT" },
            commands);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task ListMessagesAsync_EmptyUidlListingReturnsNoMessages(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token,
            uidlResponse: "+OK\r\n.\r\n");
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using (var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                .ConfigureAwait(false))
            {
                var messages = await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false);

                Assert.AreEqual(0, messages.Count);
            }
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password", "UIDL", "QUIT" }
                : new[] { "USER external-user", "PASS external-password", "UIDL", "QUIT" },
            commands);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task DownloadMessageAsync_RejectedRetrQuitsWithoutDelete(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: true,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using (var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                .ConfigureAwait(false))
            {
                var messages = await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false);
                Assert.AreEqual(1, messages.Count);
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    async () => await session.DownloadMessageAsync(messages[0], timeout.Token).ConfigureAwait(false));
            }
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password", "UIDL", "RETR 1", "QUIT" }
                : new[] { "USER external-user", "PASS external-password", "UIDL", "RETR 1", "QUIT" },
            commands);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task DownloadMessageAsync_DisconnectBeforeRetrTerminatorRemainsFatal(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token,
            disconnectDuringRetrBody: true);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using (var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                .ConfigureAwait(false))
            {
                var messages = await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false);
                Assert.AreEqual(1, messages.Count);
                await Assert.ThrowsExactlyAsync<IOException>(
                    async () => await session.DownloadMessageAsync(messages[0], timeout.Token).ConfigureAwait(false));
            }
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password", "UIDL", "RETR 1" }
                : new[] { "USER external-user", "PASS external-password", "UIDL", "RETR 1" },
            commands);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task DeleteMessageAsync_RejectedDeleStillQuits(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: true,
            disconnectOnDele: false,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using (var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                .ConfigureAwait(false))
            {
                var messages = await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false);
                Assert.AreEqual(1, messages.Count);
                await session.DownloadMessageAsync(messages[0], timeout.Token).ConfigureAwait(false);
                await session.DeleteMessageAsync(messages[0], timeout.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password", "UIDL", "RETR 1", "DELE 1", "QUIT" }
                : new[] { "USER external-user", "PASS external-password", "UIDL", "RETR 1", "DELE 1", "QUIT" },
            commands);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task DeleteMessageAsync_DisconnectBeforeResponseRemainsFatal(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: true,
            timeout.Token);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using (var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                .ConfigureAwait(false))
            {
                var messages = await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false);
                Assert.AreEqual(1, messages.Count);
                await session.DownloadMessageAsync(messages[0], timeout.Token).ConfigureAwait(false);
                await Assert.ThrowsExactlyAsync<IOException>(
                    async () => await session.DeleteMessageAsync(messages[0], timeout.Token).ConfigureAwait(false));
            }
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password", "UIDL", "RETR 1", "DELE 1" }
                : new[] { "USER external-user", "PASS external-password", "UIDL", "RETR 1", "DELE 1" },
            commands);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task DisposeAsync_RejectedQuitDoesNotThrow(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token,
            rejectQuit: true);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using (var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                .ConfigureAwait(false))
            {
                var messages = await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false);
                await session.DownloadMessageAsync(messages[0], timeout.Token).ConfigureAwait(false);
                await session.DeleteMessageAsync(messages[0], timeout.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password", "UIDL", "RETR 1", "DELE 1", "QUIT" }
                : new[] { "USER external-user", "PASS external-password", "UIDL", "RETR 1", "DELE 1", "QUIT" },
            commands);
    }

    [TestMethod]
    [DataRow(ExternalFetchConnectionSecurity.None, false)]
    [DataRow(ExternalFetchConnectionSecurity.StartTlsOptional, true)]
    public async Task DisposeAsync_DisconnectBeforeQuitResponseDoesNotThrow(
        ExternalFetchConnectionSecurity connectionSecurity,
        bool expectCapa)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var commands = new List<string>();
        var serverTask = RunPop3ServerAsync(
            listener,
            commands,
            rejectRetr: false,
            rejectDele: false,
            disconnectOnDele: false,
            timeout.Token,
            disconnectOnQuit: true);
        try
        {
            var factory = new TcpExternalFetchSessionFactory(new ExternalFetchPop3ClientOptions { EnforceEgressPolicy = false });
            await using (var session = await factory
                .ConnectAsync(CreateAccount(endpoint.Port, connectionSecurity), timeout.Token)
                .ConfigureAwait(false))
            {
                var messages = await session.ListMessagesAsync(timeout.Token).ConfigureAwait(false);
                await session.DownloadMessageAsync(messages[0], timeout.Token).ConfigureAwait(false);
                await session.DeleteMessageAsync(messages[0], timeout.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            listener.Stop();
        }

        await serverTask.ConfigureAwait(false);
        CollectionAssert.AreEqual(
            expectCapa
                ? new[] { "CAPA", "USER external-user", "PASS external-password", "UIDL", "RETR 1", "DELE 1", "QUIT" }
                : new[] { "USER external-user", "PASS external-password", "UIDL", "RETR 1", "DELE 1", "QUIT" },
            commands);
    }

    private static byte[] CreateOkControlLine(int totalBytes)
    {
        const int framingBytes = 6;
        return Encoding.ASCII.GetBytes("+OK " + new string('x', totalBytes - framingBytes) + "\r\n");
    }

    private static async Task RunControlServerAsync(
        TcpListener listener,
        byte[] greeting,
        bool stallAfterGreeting,
        bool stallAfterUser,
        bool stallAfterQuit,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        if (greeting.Length > 0)
        {
            await stream.WriteAsync(greeting, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        if (stallAfterGreeting)
        {
            await WaitForDisconnectAsync(stream, cancellationToken).ConfigureAwait(false);
            return;
        }

        while (true)
        {
            var command = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
            if (command.StartsWith("USER ", StringComparison.OrdinalIgnoreCase) && stallAfterUser)
            {
                await WaitForDisconnectAsync(stream, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (command.StartsWith("USER ", StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith("PASS ", StringComparison.OrdinalIgnoreCase))
            {
                await WriteRawAsync(stream, "+OK\r\n", cancellationToken).ConfigureAwait(false);
            }
            else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                if (stallAfterQuit)
                {
                    await WaitForDisconnectAsync(stream, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await WriteRawAsync(stream, "+OK bye\r\n", cancellationToken).ConfigureAwait(false);
                return;
            }
            else
            {
                await WriteRawAsync(stream, "-ERR unexpected\r\n", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task WaitForDisconnectAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            await ReadUntilDisconnectAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    private static async Task RunPop3ServerAsync(
        TcpListener listener,
        List<string> commands,
        bool rejectRetr,
        bool rejectDele,
        bool disconnectOnDele,
        CancellationToken cancellationToken,
        bool rejectQuit = false,
        bool disconnectOnQuit = false,
        bool disconnectDuringUidlListing = false,
        bool disconnectDuringRetrBody = false,
        string? uidlResponse = null,
        bool emptyRetrBody = false)
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
                if (disconnectDuringUidlListing)
                {
                    await WriteRawAsync(stream, "+OK\r\n1 uid-1\r\n", cancellationToken).ConfigureAwait(false);
                    client.Client.Shutdown(SocketShutdown.Both);
                    break;
                }

                await WriteRawAsync(stream, uidlResponse ?? "+OK\r\n1 uid-1\r\n.\r\n", cancellationToken).ConfigureAwait(false);
            }
            else if (command.Equals("RETR 1", StringComparison.OrdinalIgnoreCase))
            {
                if (disconnectDuringRetrBody)
                {
                    await WriteRawAsync(stream, "+OK\r\nSubject: fetched\r\n", cancellationToken).ConfigureAwait(false);
                    client.Client.Shutdown(SocketShutdown.Both);
                    break;
                }

                var response = rejectRetr
                    ? "-ERR message unavailable\r\n"
                    : emptyRetrBody
                        ? "+OK\r\n.\r\n"
                        : "+OK\r\nSubject: fetched\r\n..dot-stuffed\r\n.\r\n";
                await WriteRawAsync(stream, response, cancellationToken).ConfigureAwait(false);
            }
            else if (command.Equals("DELE 1", StringComparison.OrdinalIgnoreCase))
            {
                if (disconnectOnDele)
                {
                    client.Client.Shutdown(SocketShutdown.Both);
                    break;
                }

                var response = rejectDele
                    ? "-ERR delete denied\r\n"
                    : "+OK\r\n";
                await WriteRawAsync(stream, response, cancellationToken).ConfigureAwait(false);
            }
            else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                if (disconnectOnQuit)
                {
                    client.Client.Shutdown(SocketShutdown.Both);
                    break;
                }

                var response = rejectQuit
                    ? "-ERR quit denied\r\n"
                    : "+OK bye\r\n";
                await WriteRawAsync(stream, response, cancellationToken).ConfigureAwait(false);
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
        bool rejectCapa,
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
                var response = rejectCapa
                    ? "-ERR CAPA unavailable\r\n"
                    : "+OK capability list follows\r\nUIDL\r\nUSER\r\n.\r\n";
                await WriteRawAsync(stream, response, cancellationToken).ConfigureAwait(false);
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

    private static async Task RunPop3ServerRejectingStlsAsync(
        TcpListener listener,
        List<string> commands,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        await WriteRawAsync(stream, "+OK fake server ready\r\n", cancellationToken).ConfigureAwait(false);

        var capaCommand = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
        commands.Add(capaCommand);
        await WriteRawAsync(
            stream,
            "+OK capability list follows\r\nSTLS\r\nUIDL\r\nUSER\r\n.\r\n",
            cancellationToken).ConfigureAwait(false);

        var stlsCommand = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
        commands.Add(stlsCommand);
        await WriteRawAsync(stream, "-ERR TLS unavailable\r\n", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> RunPop3ServerRejectingGreetingAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        await WriteRawAsync(stream, "-ERR access denied\r\n", cancellationToken).ConfigureAwait(false);
        client.Client.Shutdown(SocketShutdown.Send);

        return await ReadUntilDisconnectAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> RunPop3ServerRejectingAuthenticationAsync(
        TcpListener listener,
        List<string> commands,
        bool expectCapa,
        bool rejectUser,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        await WriteRawAsync(stream, "+OK fake server ready\r\n", cancellationToken).ConfigureAwait(false);

        if (expectCapa)
        {
            commands.Add(await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false));
            await WriteRawAsync(
                stream,
                "+OK capability list follows\r\nUIDL\r\nUSER\r\n.\r\n",
                cancellationToken).ConfigureAwait(false);
        }

        commands.Add(await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false));
        if (rejectUser)
        {
            await WriteRawAsync(stream, "-ERR invalid user\r\n", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteRawAsync(stream, "+OK user accepted\r\n", cancellationToken).ConfigureAwait(false);
            commands.Add(await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false));
            await WriteRawAsync(stream, "-ERR invalid password\r\n", cancellationToken).ConfigureAwait(false);
        }

        client.Client.Shutdown(SocketShutdown.Send);

        return await ReadUntilDisconnectAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadUntilDisconnectAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var received = new MemoryStream();
        var buffer = new byte[128];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return received.ToArray();
            }

            received.Write(buffer, 0, count);
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

    private sealed class RecordingAddressResolver(IReadOnlyList<IPAddress> addresses) : IExternalFetchAddressResolver
    {
        public int CallCount { get; private set; }

        public string? LastHostName { get; private set; }

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
            string hostName,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastHostName = hostName;
            return ValueTask.FromResult(addresses);
        }
    }

    private sealed class StallingAddressResolver : IExternalFetchAddressResolver
    {
        public async ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
            string hostName,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return [];
        }
    }
}
