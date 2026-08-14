using Microsoft.Extensions.DependencyInjection;
using HMailServer.Protocols.Imap;
using HMailServer.Protocols.Pop3;
using HMailServer.Protocols.Smtp;

namespace HMailServer.Service;

public static class HMailServerServiceCollectionExtensions
{
    public static IServiceCollection AddProductionHostedServices(
        this IServiceCollection services,
        bool externalFetchEnabled,
        bool enableComLocalServer = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ServerReadinessSignal>();
        services.AddSingleton<ServiceReinitializationCoordinator>(provider =>
            new ServiceReinitializationCoordinator(
                provider.GetRequiredService<ServerReadinessSignal>()));
        services.AddHostedService<ServerBootstrapper>();
        services.AddHostedService<BackupTaskHostedService>();
        services.AddHostedService<MessageSearchBackfillHostedService>();
        services.AddHostedService<DeliveryQueueProcessorHostedService>();
        services.AddHostedService<DeliveryQueueStatusMaintenanceHostedService>();
        if (externalFetchEnabled)
        {
            services.AddHostedService<ExternalFetchHostedService>();
        }
        services.AddHostedService<ImapTcpListenerHostedService>();
        services.AddHostedService<Pop3TcpListenerHostedService>();
        services.AddHostedService<SmtpTcpListenerHostedService>();
        services.AddSingleton<IReadOnlyList<Task>>(provider =>
        {
            var listenerStartupTasks = new List<Task>(capacity: 3);
            if (provider.GetRequiredService<ImapTcpListenerOptions>().Enabled)
            {
                listenerStartupTasks.Add(provider.GetRequiredService<ImapTcpListener>().Started);
            }

            if (provider.GetRequiredService<Pop3TcpListenerOptions>().Enabled)
            {
                listenerStartupTasks.Add(provider.GetRequiredService<Pop3TcpListener>().Started);
            }

            if (provider.GetRequiredService<SmtpTcpListenerOptions>().Enabled)
            {
                listenerStartupTasks.Add(provider.GetRequiredService<SmtpTcpListener>().Started);
            }

            return listenerStartupTasks;
        });
        services.AddHostedService<ServerStartupCoordinator>();
        if (enableComLocalServer)
        {
            services.AddHostedService<ComLocalServerHostedService>();
        }

        return services;
    }
}
