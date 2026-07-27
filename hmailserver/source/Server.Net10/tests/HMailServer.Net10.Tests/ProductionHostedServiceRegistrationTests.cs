using HMailServer.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ProductionHostedServiceRegistrationTests
{
    [TestMethod]
    public void AddProductionHostedServices_PreservesRegistrationOrder()
    {
        foreach (var externalFetchEnabled in new[] { true, false })
        {
            var services = new ServiceCollection();

            services.AddProductionHostedServices(externalFetchEnabled);

            var descriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .ToArray();
            var expectedTypes = new List<Type>
            {
                typeof(ServerBootstrapper),
                typeof(BackupTaskHostedService),
                typeof(MessageSearchBackfillHostedService),
                typeof(DeliveryQueueProcessorHostedService),
                typeof(DeliveryQueueStatusMaintenanceHostedService)
            };
            if (externalFetchEnabled)
            {
                expectedTypes.Add(typeof(ExternalFetchHostedService));
            }
            expectedTypes.Add(typeof(ImapTcpListenerHostedService));
            expectedTypes.Add(typeof(Pop3TcpListenerHostedService));
            expectedTypes.Add(typeof(SmtpTcpListenerHostedService));
            expectedTypes.Add(typeof(ServerStartupCoordinator));
            expectedTypes.Add(GetServiceType("ComLocalServerHostedService"));

            CollectionAssert.AreEqual(
                expectedTypes,
                descriptors.Select(descriptor => descriptor.ImplementationType).ToArray());
        }
    }

    private static Type GetServiceType(string name) =>
        typeof(ServerBootstrapper).Assembly.GetType($"HMailServer.Service.{name}", throwOnError: true)!;
}
