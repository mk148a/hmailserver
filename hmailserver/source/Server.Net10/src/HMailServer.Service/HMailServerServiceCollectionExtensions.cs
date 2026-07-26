using Microsoft.Extensions.DependencyInjection;

namespace HMailServer.Service;

public static class HMailServerServiceCollectionExtensions
{
    public static IServiceCollection AddProductionHostedServices(
        this IServiceCollection services,
        bool externalFetchEnabled)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHostedService<ComLocalServerHostedService>();
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

        return services;
    }
}
