using System.Net;
using System.Net.Sockets;
using HMailServer.Core.Abstractions;
using HMailServer.ComInterop;
using HMailServer.Delivery;
using HMailServer.Protocols.Pop3;
using HMailServer.Protocols.Smtp;
using HMailServer.Scripting;
using HMailServer.Service;
using Microsoft.Extensions.DependencyInjection;
using Hosting = Microsoft.Extensions.Hosting;
using System.Reflection;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ProductionHostCompositionTests
{
    [TestMethod]
    public void HostBuild_ExternalFetchEgressPolicyDefaultsToEnforcedAndHonorsExplicitOverride()
    {
        foreach (var (egressEnforce, expected) in new (string? Value, bool Expected)[]
        {
            (null, true),
            ("false", false)
        })
        {
            var dataDirectory = Path.Combine(
                Path.GetTempPath(),
                $"hmailserver-net10-host-external-fetch-egress-{Guid.NewGuid():N}");
            var initializationFile = Path.Combine(dataDirectory, "hMailServer.ini");
            var args = new List<string>
            {
                "--ConnectionStrings:hMailServer=Server=127.0.0.1;Database=NeverOpened;Integrated Security=False;User Id=never;Password=never;TrustServerCertificate=True",
                $"--DataDirectory={dataDirectory}",
                $"--InitializationFile={initializationFile}",
                "--Imap:Enabled=false",
                "--Pop3:Enabled=false",
                "--Smtp:Enabled=false",
                "--ExternalFetch:Enabled=false"
            };
            if (egressEnforce is not null)
            {
                args.Add($"--ExternalFetch:EgressEnforce={egressEnforce}");
            }

            var composition = HMailServer.Service.Host.Build(args.ToArray());

            using var host = composition.Host;
            Assert.AreEqual(
                expected,
                host.Services.GetRequiredService<ExternalFetchPop3ClientOptions>().EnforceEgressPolicy);
        }
    }

    [TestMethod]
    public void HostBuild_ResolvesScriptExecutorWhenScriptingIsDisabled()
    {
        var dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"hmailserver-net10-host-scripting-disabled-{Guid.NewGuid():N}");
        var initializationFile = Path.Combine(dataDirectory, "hMailServer.ini");

        var composition = HMailServer.Service.Host.Build(
            [
                "--ConnectionStrings:hMailServer=Server=127.0.0.1;Database=NeverOpened;Integrated Security=False;User Id=never;Password=never;TrustServerCertificate=True",
                $"--DataDirectory={dataDirectory}",
                $"--InitializationFile={initializationFile}",
                "--Imap:Enabled=false",
                "--Pop3:Enabled=false",
                "--Smtp:Enabled=false",
                "--ExternalFetch:Enabled=false",
                "--Scripting:Enabled=false"
            ]);

        using var host = composition.Host;
        Assert.IsNotNull(host.Services.GetRequiredService<WindowsScriptRuleExecutor>());
        Assert.IsFalse(host.Services.GetService<ISmtpRuleScriptExecutor>() is not null);
    }

    [TestMethod]
    public void HostBuild_WiresAccountSizeInvalidationIntoImapMutationStores()
    {
        var dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"hmailserver-net10-host-account-size-{Guid.NewGuid():N}");
        var initializationFile = Path.Combine(dataDirectory, "hMailServer.ini");

        var composition = HMailServer.Service.Host.Build(
            [
                "--ConnectionStrings:hMailServer=Server=127.0.0.1;Database=NeverOpened;Integrated Security=False;User Id=never;Password=never;TrustServerCertificate=True",
                $"--DataDirectory={dataDirectory}",
                $"--InitializationFile={initializationFile}",
                "--Imap:Enabled=false",
                "--Pop3:Enabled=false",
                "--Smtp:Enabled=false",
                "--ExternalFetch:Enabled=false"
            ]);

        using var host = composition.Host;
        foreach (var store in new object[]
        {
            host.Services.GetRequiredService<IImapMessageAppendStore>(),
            host.Services.GetRequiredService<IImapMessageCopyStore>(),
            host.Services.GetRequiredService<IImapMessageMutationStore>(),
            host.Services.GetRequiredService<IScriptMessageCopyStore>(),
            host.Services.GetRequiredService<IImportMessageFromFileStore>()
        })
        {
            var callback = store.GetType()
                .GetField("_accountSizeInvalidationCallback", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(store) as Action<int>;

            Assert.IsNotNull(callback);
            Assert.AreEqual(typeof(AccountAdministrationRuntimeHost), callback.Method.DeclaringType);
            Assert.AreEqual(nameof(AccountAdministrationRuntimeHost.InvalidateAccountSize), callback.Method.Name);
        }
    }

    [TestMethod]
    public void HostBuild_ResolvesEveryRegisteredHostedServiceWithoutStartingOrUsingDatabase()
    {
        foreach (var externalFetchEnabled in new[] { true, false })
        {
            var dataDirectory = Path.Combine(
                Path.GetTempPath(),
                $"hmailserver-net10-host-composition-{Guid.NewGuid():N}");
            var initializationFile = Path.Combine(dataDirectory, "hMailServer.ini");

            var composition = HMailServer.Service.Host.Build(
                [
                    "--ConnectionStrings:hMailServer=Server=127.0.0.1;Database=NeverOpened;Integrated Security=False;User Id=never;Password=never;TrustServerCertificate=True",
                    $"--DataDirectory={dataDirectory}",
                    $"--InitializationFile={initializationFile}",
                    "--Imap:Enabled=false",
                    "--Pop3:Enabled=false",
                    "--Smtp:Enabled=false",
                    $"--ExternalFetch:Enabled={externalFetchEnabled.ToString().ToLowerInvariant()}"
                ]);

            using var host = composition.Host;
            Assert.IsNotNull(host.Services.GetRequiredService<SmtpSessionOptions>().GreetingProvider);
            var hostedServices = host.Services
                .GetServices<Hosting.IHostedService>()
                .ToArray();

            var expectedNames = new List<string>
            {
                nameof(ServerBootstrapper),
                nameof(BackupTaskHostedService),
                nameof(MessageSearchBackfillHostedService),
                nameof(DeliveryQueueProcessorHostedService),
                nameof(DeliveryQueueStatusMaintenanceHostedService)
            };
            if (externalFetchEnabled)
            {
                expectedNames.Add(nameof(ExternalFetchHostedService));
            }
            expectedNames.Add(nameof(ImapTcpListenerHostedService));
            expectedNames.Add(nameof(Pop3TcpListenerHostedService));
            expectedNames.Add(nameof(SmtpTcpListenerHostedService));
            expectedNames.Add(nameof(ServerStartupCoordinator));
            expectedNames.Add("ComLocalServerHostedService");

            CollectionAssert.AreEqual(
                expectedNames,
                hostedServices.Select(service => service.GetType().Name).ToArray());
            Assert.IsFalse(
                host.Services.GetRequiredService<Hosting.IHostApplicationLifetime>()
                    .ApplicationStarted.IsCancellationRequested);
            Assert.IsFalse(ReferenceEquals(
                host.Services.GetRequiredService<IDeliveryQueueWakeSignal>(),
                host.Services.GetRequiredService<IExternalFetchWakeSignal>()));
            Assert.IsFalse(Directory.Exists(dataDirectory));
        }
    }

    [TestMethod]
    public void HostBuild_RemoteSmtpLocalEndpointPolicyUsesEnabledListenerOptionsOnly()
    {
        using var unrelatedListener = new TcpListener(IPAddress.Loopback, 0);
        unrelatedListener.Start();
        var disabledPop3Port = ((IPEndPoint)unrelatedListener.LocalEndpoint).Port;
        var dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"hmailserver-net10-host-listener-policy-{Guid.NewGuid():N}");
        var initializationFile = Path.Combine(dataDirectory, "hMailServer.ini");

        var composition = HMailServer.Service.Host.Build(
            [
                "--ConnectionStrings:hMailServer=Server=127.0.0.1;Database=NeverOpened;Integrated Security=False;User Id=never;Password=never;TrustServerCertificate=True",
                $"--DataDirectory={dataDirectory}",
                $"--InitializationFile={initializationFile}",
                "--Imap:Enabled=true",
                "--Imap:BindAddress=127.0.0.1",
                "--Imap:Port=2143",
                "--Smtp:Enabled=true",
                "--Smtp:BindAddress=::1",
                "--Smtp:Port=2525",
                "--Pop3:Enabled=false",
                "--Pop3:BindAddress=127.0.0.1",
                $"--Pop3:Port={disabledPop3Port}",
                "--ExternalFetch:Enabled=false"
            ]);

        using var host = composition.Host;
        var policy = host.Services.GetRequiredService<RemoteSmtpLocalEndpointPolicy>();

        Assert.ThrowsExactly<RemoteSmtpLocalEndpointDeniedException>(() => policy.EnsureAllowed(
            CreateGuardedEndpoint(IPAddress.Loopback, 2143)));
        Assert.ThrowsExactly<RemoteSmtpLocalEndpointDeniedException>(() => policy.EnsureAllowed(
            CreateGuardedEndpoint(IPAddress.IPv6Loopback, 2525)));
        policy.EnsureAllowed(CreateGuardedEndpoint(IPAddress.Loopback, disabledPop3Port));
    }

    [TestMethod]
    public async Task ComLocalServerHostedService_WaitsForReadinessBeforeStartingCom()
    {
        var dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"hmailserver-net10-com-readiness-{Guid.NewGuid():N}");
        var initializationFile = Path.Combine(dataDirectory, "hMailServer.ini");

        var composition = HMailServer.Service.Host.Build(
            [
                "--ConnectionStrings:hMailServer=Server=127.0.0.1;Database=NeverOpened;Integrated Security=False;User Id=never;Password=never;TrustServerCertificate=True",
                $"--DataDirectory={dataDirectory}",
                $"--InitializationFile={initializationFile}",
                "--Imap:Enabled=false",
                "--Pop3:Enabled=false",
                "--Smtp:Enabled=false",
                "--ExternalFetch:Enabled=false"
            ]);

        using var host = composition.Host;
        var readiness = host.Services.GetRequiredService<ServerReadinessSignal>();
        var comService = host.Services
            .GetServices<Hosting.IHostedService>()
            .Single(service => service.GetType().Name == "ComLocalServerHostedService");
        using var cancellation = new CancellationTokenSource();

        var startTask = comService.StartAsync(cancellation.Token);

        Assert.IsFalse(startTask.IsCompleted);

        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => startTask);
        Assert.IsFalse(readiness.WaitAsync(CancellationToken.None).IsCompleted);
        Assert.IsFalse(Directory.Exists(dataDirectory));
    }

    private static RemoteSmtpEndpoint CreateGuardedEndpoint(IPAddress address, int port) =>
        new(
            "dns-derived.example",
            port,
            RemoteSmtpConnectionSecurity.None,
            ConnectionAddress: address.ToString(),
            EnforceLocalEndpointGuard: true);
}
