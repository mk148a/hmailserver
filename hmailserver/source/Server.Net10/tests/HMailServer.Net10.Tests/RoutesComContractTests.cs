using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RoutesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceRoutes),
            "111F318A-C087-4091-BD1F-4226230EE513",
            new[]
            {
                "get_Item", "get_Count", "DeleteByDBID", "Add", "get_ItemByName",
                "get_ItemByDBID", "Refresh"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceRoutes).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            6,
            typeof(IInterfaceRoutes).GetMethod("Refresh")?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceRoute),
            "F78FA851-D3D3-4A28-AFCC-A471C00781D3",
            new[]
            {
                "get_ID", "get_DomainName", "set_DomainName", "get_TargetSMTPHost",
                "set_TargetSMTPHost", "get_TargetSMTPPort", "set_TargetSMTPPort",
                "get_NumberOfTries", "set_NumberOfTries", "get_MinutesBetweenTry",
                "set_MinutesBetweenTry", "get_AllAddresses", "set_AllAddresses", "get_Addresses",
                "get_RelayerRequiresAuth", "set_RelayerRequiresAuth", "get_RelayerAuthUsername",
                "set_RelayerAuthUsername", "SetRelayerAuthPassword", "get_TreatSecurityAsLocalDomain",
                "set_TreatSecurityAsLocalDomain", "Save", "get_UseSSL", "set_UseSSL",
                "get_Description", "set_Description", "Delete", "get_TreatSenderAsLocalDomain",
                "set_TreatSenderAsLocalDomain", "get_TreatRecipientAsLocalDomain",
                "set_TreatRecipientAsLocalDomain", "get_ConnectionSecurity", "set_ConnectionSecurity"
            });
        Assert.AreEqual(
            19,
            typeof(IInterfaceRoute).GetProperty(nameof(IInterfaceRoute.ConnectionSecurity))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<Routes>(
            "7D174A9D-D44C-4627-BE78-E5DDC513C31F",
            "hMailServer.Routes.1",
            typeof(IInterfaceRoutes));
        AssertComClass<Route>(
            "3FF9BB08-7924-4418-BADA-7D959467D51B",
            "hMailServer.Route.1",
            typeof(IInterfaceRoute));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var routesError = Assert.ThrowsExactly<COMException>(() => _ = new Routes().Count);
        var routeError = Assert.ThrowsExactly<COMException>(() => _ = new Route().DomainName);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Routes);

        Assert.AreEqual(EAccessDenied, routesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, routeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlyNonSecretSnapshotsAndLegacyLookupErrors()
    {
        IInterfaceRoutes routes = Routes.CreateAuthorized(
            new[]
            {
                Snapshot(10, "alpha.example", ComConnectionSecurity.Tls),
                Snapshot(20, "beta.example", ComConnectionSecurity.StartTlsRequired)
            });

        Assert.AreEqual(2, routes.Count);
        AssertRoute(routes[0], 10, "alpha.example", ComConnectionSecurity.Tls, useSsl: true);
        AssertRoute(
            routes.get_ItemByName("BETA.EXAMPLE"),
            20,
            "beta.example",
            ComConnectionSecurity.StartTlsRequired,
            useSsl: false);
        Assert.AreEqual(20, routes.get_ItemByDBID(20).ID);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = routes[2]);
        var badName = Assert.ThrowsExactly<COMException>(() => _ = routes.get_ItemByName("missing.example"));
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = routes.get_ItemByDBID(30));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => routes.Add());
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => routes.DeleteByDBID(10));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(routes.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => routes[0].DomainName = "changed.example");
        var pendingAddresses = Assert.ThrowsExactly<COMException>(() => _ = routes[0].Addresses);
        var pendingPassword = Assert.ThrowsExactly<COMException>(() => routes[0].SetRelayerAuthPassword("secret"));
        var pendingSave = Assert.ThrowsExactly<COMException>(routes[0].Save);
        var pendingRouteDelete = Assert.ThrowsExactly<COMException>(routes[0].Delete);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAddresses.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingPassword.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRouteDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredRouteRuntime()
    {
        RouteAdministrationRuntimeHost.Configure(
            new FixedRouteAdministrationStore(
                new[]
                {
                    Snapshot(20, "beta.example", ComConnectionSecurity.StartTlsRequired),
                    Snapshot(10, "alpha.example", ComConnectionSecurity.Tls)
                }));
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var routes = settings.Routes;

        Assert.AreEqual(2, routes.Count);
        Assert.AreEqual("alpha.example", routes[0].DomainName);
    }

    private static RouteAdministrationSnapshot Snapshot(
        int id,
        string domainName,
        ComConnectionSecurity connectionSecurity) =>
        new(
            id,
            domainName,
            $"Route for {domainName}",
            $"smtp.{domainName}",
            2525,
            4,
            15,
            true,
            true,
            "relay-user",
            true,
            false,
            (int)connectionSecurity);

    private static void AssertRoute(
        IInterfaceRoute route,
        int id,
        string domainName,
        ComConnectionSecurity connectionSecurity,
        bool useSsl)
    {
        Assert.AreEqual(id, route.ID);
        Assert.AreEqual(domainName, route.DomainName);
        Assert.AreEqual($"Route for {domainName}", route.Description);
        Assert.AreEqual($"smtp.{domainName}", route.TargetSMTPHost);
        Assert.AreEqual(2525, route.TargetSMTPPort);
        Assert.AreEqual(4, route.NumberOfTries);
        Assert.AreEqual(15, route.MinutesBetweenTry);
        Assert.IsTrue(route.AllAddresses);
        Assert.IsTrue(route.RelayerRequiresAuth);
        Assert.AreEqual("relay-user", route.RelayerAuthUsername);
        Assert.IsTrue(route.TreatSecurityAsLocalDomain);
        Assert.IsTrue(route.TreatRecipientAsLocalDomain);
        Assert.IsFalse(route.TreatSenderAsLocalDomain);
        Assert.AreEqual(connectionSecurity, route.ConnectionSecurity);
        Assert.AreEqual(useSsl, route.UseSSL);
    }

    private static void AssertContract(Type contract, string interfaceId, string[] methodNames)
    {
        Assert.AreEqual(new Guid(interfaceId), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            methodNames,
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());
    }

    private static void AssertComClass<T>(string classId, string progId, Type defaultInterface)
    {
        var type = typeof(T);

        Assert.AreEqual(new Guid(classId), type.GUID);
        Assert.AreEqual(progId, type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(defaultInterface, type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    private sealed class FixedRouteAdministrationStore(IReadOnlyList<RouteAdministrationSnapshot> routes)
        : IRouteAdministrationStore
    {
        public ValueTask<IReadOnlyList<RouteAdministrationSnapshot>> GetRoutesAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<RouteAdministrationSnapshot>>(
                routes.OrderBy(route => route.DomainName, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
