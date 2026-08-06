using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RoutesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
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
        var routesRefreshError = Assert.ThrowsExactly<COMException>(new Routes().Refresh);
        var routeError = Assert.ThrowsExactly<COMException>(() => _ = new Route().DomainName);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Routes);

        Assert.AreEqual(EAccessDenied, routesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, routesRefreshError.ErrorCode);
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
        Assert.AreEqual(ENotImplemented, pendingPassword.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRouteDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceRoutes routes = Routes.CreateAuthorized(
            new[]
            {
                Snapshot(10, "alpha.example", ComConnectionSecurity.Tls)
            },
            () =>
            {
                reloads++;
                if (failReload)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }

                return new[]
                {
                    Snapshot(20, "beta.example", ComConnectionSecurity.StartTlsRequired),
                    Snapshot(30, "gamma.example", ComConnectionSecurity.Tls)
                };
            });

        Assert.AreEqual(1, routes.Count);
        Assert.AreEqual("alpha.example", routes[0].DomainName);

        routes.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, routes.Count);
        AssertRoute(
            routes[0],
            20,
            "beta.example",
            ComConnectionSecurity.StartTlsRequired,
            useSsl: false);
        Assert.AreEqual(30, routes.get_ItemByName("GAMMA.EXAMPLE").ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = routes.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(routes.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, routes.Count);
        Assert.AreEqual("beta.example", routes.get_ItemByDBID(20).DomainName);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredRouteRuntime()
    {
        var store = new MutableRouteAdministrationStore(
            new[]
            {
                Snapshot(20, "beta.example", ComConnectionSecurity.StartTlsRequired),
                Snapshot(10, "alpha.example", ComConnectionSecurity.Tls)
            });
        RouteAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var routes = settings.Routes;

        Assert.AreEqual(2, routes.Count);
        Assert.AreEqual("alpha.example", routes[0].DomainName);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(30, "gamma.example", ComConnectionSecurity.Tls),
                Snapshot(20, "beta.example", ComConnectionSecurity.StartTlsRequired)
            });

        routes.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, routes.Count);
        Assert.AreEqual("beta.example", routes[0].DomainName);
        Assert.AreEqual(30, routes.get_ItemByDBID(30).ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = routes.get_ItemByDBID(10)).ErrorCode);
    }


    [TestMethod]
    public void AuthorizedCollection_AddStagesLegacyDefaultsAndSavePublishesInsertedIdentity()
    {
        var inserted = new List<RouteAdministrationSnapshot>();
        var nextId = 100;
        IInterfaceRoutes routes = Routes.CreateAuthorized(
            new[] { Snapshot(10, "alpha.example", ComConnectionSecurity.Tls) },
            insert: route =>
            {
                inserted.Add(route);
                return ++nextId;
            });

        var draft = routes.Add();

        Assert.AreEqual(0, draft.ID);
        Assert.IsTrue(draft.AllAddresses);
        Assert.IsFalse(draft.RelayerRequiresAuth);
        Assert.AreEqual(string.Empty, draft.DomainName);
        Assert.AreEqual(string.Empty, draft.Description);
        Assert.AreEqual(0, draft.TargetSMTPPort);
        Assert.AreEqual(0, draft.NumberOfTries);
        Assert.AreEqual(0, draft.MinutesBetweenTry);
        Assert.AreEqual(ComConnectionSecurity.None, draft.ConnectionSecurity);

        draft.DomainName = "relay.example";
        draft.Description = "Relay";
        draft.TargetSMTPHost = "smtp.relay.example";
        draft.TargetSMTPPort = 2525;
        draft.NumberOfTries = 5;
        draft.MinutesBetweenTry = 20;
        draft.AllAddresses = false;
        draft.RelayerRequiresAuth = true;
        draft.RelayerAuthUsername = "relay-user";
        draft.SetRelayerAuthPassword("secret");
        draft.TreatSecurityAsLocalDomain = true;
        draft.TreatSenderAsLocalDomain = true;
        draft.UseSSL = true;
        draft.ConnectionSecurity = ComConnectionSecurity.StartTlsRequired;

        Assert.AreEqual(1, routes.Count);
        draft.Save();

        Assert.AreEqual(2, routes.Count);
        Assert.AreEqual(101, draft.ID);
        Assert.AreEqual(1, inserted.Count);
        var persisted = inserted[0];
        Assert.AreEqual(0, persisted.Id);
        Assert.AreEqual("relay.example", persisted.DomainName);
        Assert.AreEqual("Relay", persisted.Description);
        Assert.AreEqual("smtp.relay.example", persisted.TargetSmtpHost);
        Assert.AreEqual(2525, persisted.TargetSmtpPort);
        Assert.AreEqual(5, persisted.NumberOfTries);
        Assert.AreEqual(20, persisted.MinutesBetweenTry);
        Assert.IsFalse(persisted.AllAddresses);
        Assert.IsTrue(persisted.RelayerRequiresAuth);
        Assert.AreEqual("relay-user", persisted.RelayerAuthUsername);
        Assert.AreEqual("secret", persisted.RelayerAuthPassword);
        Assert.IsTrue(persisted.TreatRecipientAsLocalDomain);
        Assert.IsTrue(persisted.TreatSenderAsLocalDomain);
        Assert.AreEqual((int)ComConnectionSecurity.StartTlsRequired, persisted.ConnectionSecurity);
        Assert.AreEqual("relay.example", routes.get_ItemByDBID(101).DomainName);
    }

    [TestMethod]
    public void FailedInsert_MapsToEFailAndRetainsDraftWithoutPublishing()
    {
        var fail = true;
        IInterfaceRoutes routes = Routes.CreateAuthorized(
            Array.Empty<RouteAdministrationSnapshot>(),
            insert: _ => fail
                ? throw new InvalidOperationException("Simulated store failure.")
                : 1);

        var draft = routes.Add();
        draft.DomainName = "relay.example";

        var saveFailure = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual(0, routes.Count);
        Assert.AreEqual(0, draft.ID);

        draft.DomainName = "other.example";
        fail = false;
        draft.Save();

        Assert.AreEqual(1, routes.Count);
        Assert.AreEqual(1, draft.ID);
        Assert.AreEqual("other.example", routes.get_ItemByDBID(1).DomainName);
    }

    [TestMethod]
    public void AddAndMutate_RecheckLiveServerAdministrator()
    {
        var authenticated = true;
        IInterfaceRoutes routes = Routes.CreateAuthorized(
            new[] { Snapshot(10, "alpha.example", ComConnectionSecurity.Tls) },
            insert: _ => 1,
            isServerAdministrator: () => authenticated);

        var draft = routes.Add();
        authenticated = false;

        var deniedAdd = Assert.ThrowsExactly<COMException>(() => routes.Add());
        var deniedSetter = Assert.ThrowsExactly<COMException>(() => draft.DomainName = "x");
        var deniedPassword = Assert.ThrowsExactly<COMException>(() => draft.SetRelayerAuthPassword("x"));
        var deniedSave = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EAccessDenied, deniedAdd.ErrorCode);
        Assert.AreEqual(EAccessDenied, deniedSetter.ErrorCode);
        Assert.AreEqual(EAccessDenied, deniedPassword.ErrorCode);
        Assert.AreEqual(EAccessDenied, deniedSave.ErrorCode);
    }

    [TestMethod]
    public void ExistingRowSave_RemainsNotImplementedUntilUpdateParity()
    {
        IInterfaceRoutes routes = Routes.CreateAuthorized(
            new[] { Snapshot(10, "alpha.example", ComConnectionSecurity.Tls) },
            insert: _ => 11);

        var existing = routes[0];
        var pendingSave = Assert.ThrowsExactly<COMException>(existing.Save);
        var pendingDelete = Assert.ThrowsExactly<COMException>(existing.Delete);
        var pendingCollectionDelete = Assert.ThrowsExactly<COMException>(() => routes.DeleteByDBID(10));

        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingCollectionDelete.ErrorCode);
    }

    [TestMethod]
    public void ExistingRowSave_PersistsStagedSettersAndReplacesCollectionSnapshot()
    {
        var updates = new List<RouteAdministrationSnapshot>();
        IInterfaceRoutes routes = Routes.CreateAuthorized(
            new[] { Snapshot(10, "alpha.example", ComConnectionSecurity.Tls) },
            insert: _ => 11,
            update: route =>
            {
                updates.Add(route);
                return true;
            });

        var existing = routes[0];
        existing.DomainName = "renamed.example";
        existing.Description = "Updated";
        existing.TargetSMTPHost = "smtp.renamed.example";
        existing.TargetSMTPPort = 25;
        existing.NumberOfTries = 2;
        existing.MinutesBetweenTry = 30;
        existing.AllAddresses = false;
        existing.RelayerRequiresAuth = true;
        existing.RelayerAuthUsername = "relay-user";
        existing.SetRelayerAuthPassword("new-secret");
        existing.TreatRecipientAsLocalDomain = true;
        existing.TreatSecurityAsLocalDomain = true;
        existing.TreatSenderAsLocalDomain = true;
        existing.UseSSL = true;
        existing.ConnectionSecurity = ComConnectionSecurity.StartTlsRequired;

        existing.Save();

        Assert.AreEqual(1, updates.Count);
        var persisted = updates[0];
        Assert.AreEqual(10, persisted.Id);
        Assert.AreEqual("renamed.example", persisted.DomainName);
        Assert.AreEqual("Updated", persisted.Description);
        Assert.AreEqual("smtp.renamed.example", persisted.TargetSmtpHost);
        Assert.AreEqual(25, persisted.TargetSmtpPort);
        Assert.AreEqual(2, persisted.NumberOfTries);
        Assert.AreEqual(30, persisted.MinutesBetweenTry);
        Assert.IsFalse(persisted.AllAddresses);
        Assert.IsTrue(persisted.RelayerRequiresAuth);
        Assert.AreEqual("relay-user", persisted.RelayerAuthUsername);
        Assert.AreEqual("new-secret", persisted.RelayerAuthPassword);
        Assert.IsTrue(persisted.TreatRecipientAsLocalDomain);
        Assert.IsTrue(persisted.TreatSenderAsLocalDomain);
        Assert.AreEqual((int)ComConnectionSecurity.StartTlsRequired, persisted.ConnectionSecurity);

        Assert.AreEqual("renamed.example", routes[0].DomainName);
        Assert.AreEqual("renamed.example", routes.get_ItemByName("RENAMED.EXAMPLE").DomainName);
        Assert.AreEqual("Updated", routes.get_ItemByDBID(10).Description);
    }

    [TestMethod]
    public void FailedUpdate_MapsToEFailAndRetainsStagedStateWithoutReplacingSnapshot()
    {
        var failUpdate = true;
        IInterfaceRoutes routes = Routes.CreateAuthorized(
            new[] { Snapshot(10, "alpha.example", ComConnectionSecurity.Tls) },
            update: _ => failUpdate
                ? throw new InvalidOperationException("Simulated store failure.")
                : true);

        var existing = routes[0];
        existing.DomainName = "changed.example";

        var saveFailure = Assert.ThrowsExactly<COMException>(existing.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual("alpha.example", routes[0].DomainName);

        existing.DomainName = "other.example";
        failUpdate = false;
        existing.Save();

        Assert.AreEqual("other.example", routes[0].DomainName);
    }

    [TestMethod]
    public void UnknownIdUpdate_MapsToEFailWhenStoreReportsNoAffectedRow()
    {
        IInterfaceRoutes routes = Routes.CreateAuthorized(
            new[] { Snapshot(10, "alpha.example", ComConnectionSecurity.Tls) },
            update: _ => false);

        var existing = routes[0];
        existing.DomainName = "changed.example";

        var saveFailure = Assert.ThrowsExactly<COMException>(existing.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
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

    private sealed class MutableRouteAdministrationStore(IReadOnlyList<RouteAdministrationSnapshot> routes)
        : IRouteAdministrationStore
    {
        private IReadOnlyList<RouteAdministrationSnapshot> _routes = routes;

        public int ReadCount { get; private set; }

        public void Replace(IReadOnlyList<RouteAdministrationSnapshot> routes)
        {
            _routes = routes;
        }

        public ValueTask<IReadOnlyList<RouteAdministrationSnapshot>> GetRoutesAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<RouteAdministrationSnapshot>>(
                _routes.OrderBy(route => route.DomainName, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }
}
