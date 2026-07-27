using HMailServer.Service;
using Microsoft.Extensions.DependencyInjection;
using Hosting = Microsoft.Extensions.Hosting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ProductionHostCompositionTests
{
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
            var hostedServices = host.Services
                .GetServices<Hosting.IHostedService>()
                .ToArray();

            var expectedNames = new List<string>
            {
                nameof(ServerBootstrapper),
                "ComLocalServerHostedService",
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

            CollectionAssert.AreEqual(
                expectedNames,
                hostedServices.Select(service => service.GetType().Name).ToArray());
            Assert.IsFalse(
                host.Services.GetRequiredService<Hosting.IHostApplicationLifetime>()
                    .ApplicationStarted.IsCancellationRequested);
            Assert.IsFalse(Directory.Exists(dataDirectory));
        }
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
}
