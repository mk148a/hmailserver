using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SurblServersComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceSURBLServers),
            "D6B91C3A-90C1-4943-B818-EE66119E4702",
            new[]
            {
                "get_Item",
                "get_Count",
                "DeleteByDBID",
                "Add",
                "get_ItemByDBID",
                "Refresh",
                "get_ItemByDNSHost"
            });
        Assert.AreEqual(0, GetProperty(typeof(IInterfaceSURBLServers), "Item").GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(5, GetMethod(typeof(IInterfaceSURBLServers), "get_ItemByDBID").GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(7, GetMethod(typeof(IInterfaceSURBLServers), "get_ItemByDNSHost").GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.BStr,
            GetMethod(typeof(IInterfaceSURBLServers), "get_ItemByDNSHost")
                .GetParameters()[0]
                .GetCustomAttribute<MarshalAsAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceSURBLServer),
            "A4866EDD-F0B8-49C7-A477-57D469F7D7D4",
            new[]
            {
                "get_Active",
                "set_Active",
                "get_ID",
                "get_DNSHost",
                "set_DNSHost",
                "get_RejectMessage",
                "set_RejectMessage",
                "Save",
                "get_Score",
                "set_Score",
                "Delete"
            });
        AssertVariantBoolProperty(typeof(IInterfaceSURBLServer), nameof(IInterfaceSURBLServer.Active), 1);
        AssertBstrProperty(typeof(IInterfaceSURBLServer), nameof(IInterfaceSURBLServer.DNSHost), 3);
        AssertBstrProperty(typeof(IInterfaceSURBLServer), nameof(IInterfaceSURBLServer.RejectMessage), 4);
        Assert.AreEqual(7, GetProperty(typeof(IInterfaceSURBLServer), nameof(IInterfaceSURBLServer.Score)).GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<SURBLServers>(
            "FCD94E5F-F05F-400B-8345-AFC7FDD6626E",
            "hMailServer.SURBLServers.1",
            typeof(IInterfaceSURBLServers));
        AssertComClass<SURBLServer>(
            "D875AEC4-7AA0-4C93-9F8F-141324C80D17",
            "hMailServer.SURBLServer.1",
            typeof(IInterfaceSURBLServer));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var collectionError = Assert.ThrowsExactly<COMException>(() => _ = new SURBLServers().Count);
        var collectionRefreshError = Assert.ThrowsExactly<COMException>(new SURBLServers().Refresh);
        var itemError = Assert.ThrowsExactly<COMException>(() => _ = new SURBLServer().DNSHost);
        var antiSpamError = Assert.ThrowsExactly<COMException>(() => _ = new AntiSpam().SURBLServers);

        Assert.AreEqual(EAccessDenied, collectionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, collectionRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, itemError.ErrorCode);
        Assert.AreEqual(EAccessDenied, antiSpamError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupResults()
    {
        IInterfaceSURBLServers servers = SURBLServers.CreateAuthorized(
            new[]
            {
                Snapshot(10, true, "multi.surbl.org", "Rejected by SURBL.", 4),
                Snapshot(20, false, "example.surbl.test", "Rejected by test SURBL.", 2)
            });

        Assert.AreEqual(2, servers.Count);
        AssertServer(servers[0], 10, true, "multi.surbl.org", "Rejected by SURBL.", 4);
        AssertServer(servers.get_ItemByDBID(20), 20, false, "example.surbl.test", "Rejected by test SURBL.", 2);
        Assert.AreEqual(10, servers.get_ItemByDNSHost("MULTI.SURBL.ORG").ID);
        Assert.IsNull(servers.get_ItemByDNSHost("missing.example.test"));

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = servers[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = servers.get_ItemByDBID(30));
        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);

        AssertPending(() => servers.Add());
        AssertPending(() => servers.DeleteByDBID(10));
        AssertPending(servers.Refresh);
        AssertPending(() => servers[0].Active = false);
        AssertPending(() => servers[0].DNSHost = "changed.example.test");
        AssertPending(() => servers[0].RejectMessage = "Changed");
        AssertPending(() => servers[0].Score = 9);
        AssertPending(servers[0].Save);
        AssertPending(servers[0].Delete);
    }

    [TestMethod]
    public void AuthorizedSurblServers_AddStagesFieldsAndPublishesOnlyAfterInsert()
    {
        SurblServerAdministrationSnapshot? inserted = null;
        IInterfaceSURBLServers servers = SURBLServers.CreateAuthorized(
            Array.Empty<SurblServerAdministrationSnapshot>(),
            insert: server =>
            {
                inserted = server;
                return 42;
            },
            isServerAdministrator: static () => true);
        var draft = servers.Add();

        draft.Active = true;
        draft.DNSHost = "multi.example.test";
        draft.RejectMessage = "Rejected";
        draft.Score = 4;

        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(0, servers.Count);

        draft.Save();

        Assert.IsNotNull(inserted);
        Assert.IsTrue(inserted!.Active);
        Assert.AreEqual("multi.example.test", inserted.DnsHost);
        Assert.AreEqual("Rejected", inserted.RejectMessage);
        Assert.AreEqual(4, inserted.Score);
        Assert.AreEqual(42, draft.ID);
        Assert.AreEqual(1, servers.Count);
        AssertServer(servers[0], 42, true, "multi.example.test", "Rejected", 4);
    }

    [TestMethod]
    public void NewSurblServer_SaveFailureRetainsDraftAndOwnerSnapshot()
    {
        IInterfaceSURBLServers servers = SURBLServers.CreateAuthorized(
            Array.Empty<SurblServerAdministrationSnapshot>(),
            insert: static _ => throw new InvalidOperationException("Simulated insert failure."),
            isServerAdministrator: static () => true);
        var draft = servers.Add();
        draft.DNSHost = "failed.example.test";

        var error = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual("failed.example.test", draft.DNSHost);
        Assert.AreEqual(0, servers.Count);
    }

    [TestMethod]
    public void NewSurblServer_RechecksLiveAdministratorBeforeMutationAndSave()
    {
        var isAdministrator = true;
        IInterfaceSURBLServers servers = SURBLServers.CreateAuthorized(
            Array.Empty<SurblServerAdministrationSnapshot>(),
            insert: static _ => 42,
            isServerAdministrator: () => isAdministrator);
        var draft = servers.Add();

        isAdministrator = false;

        var setterError = Assert.ThrowsExactly<COMException>(() => draft.DNSHost = "denied.example.test");
        var saveError = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EAccessDenied, setterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, saveError.ErrorCode);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(0, servers.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceSURBLServers servers = SURBLServers.CreateAuthorized(
            new[]
            {
                Snapshot(10, true, "multi.surbl.org", "Rejected by SURBL.", 4)
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
                    Snapshot(20, false, "example.surbl.test", "Rejected by test SURBL.", 2),
                    Snapshot(30, true, "surbl.example.test", "Rejected by example SURBL.", 5)
                };
            });

        Assert.AreEqual(1, servers.Count);
        Assert.AreEqual("multi.surbl.org", servers[0].DNSHost);

        servers.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, servers.Count);
        AssertServer(servers[0], 20, false, "example.surbl.test", "Rejected by test SURBL.", 2);
        Assert.AreEqual(30, servers.get_ItemByDNSHost("SURBL.EXAMPLE.TEST").ID);
        Assert.IsNull(servers.get_ItemByDNSHost("multi.surbl.org"));
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = servers.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(servers.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, servers.Count);
        Assert.AreEqual("Rejected by example SURBL.", servers.get_ItemByDBID(30).RejectMessage);
    }

    [TestMethod]
    public void AuthorizedAntiSpam_UsesConfiguredSurblServerRuntime()
    {
        var store = new MutableSurblServerAdministrationStore(
            new[]
            {
                Snapshot(20, false, "example.surbl.test", "Rejected by test SURBL.", 2),
                Snapshot(10, true, "multi.surbl.org", "Rejected by SURBL.", 4)
            });
        SurblServerAdministrationRuntimeHost.Configure(
            store);
        var antiSpam = AntiSpam.CreateAuthorized(new AntiSpamAdministrationSnapshot());

        var servers = antiSpam.SURBLServers;

        Assert.AreEqual(2, servers.Count);
        Assert.AreEqual(10, servers[0].ID);
        Assert.AreEqual("Rejected by test SURBL.", servers.get_ItemByDBID(20).RejectMessage);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(20, false, "example.surbl.test", "Rejected by test SURBL.", 2),
                Snapshot(30, true, "surbl.example.test", "Rejected by example SURBL.", 5)
            });

        servers.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, servers.Count);
        Assert.AreEqual(20, servers[0].ID);
        Assert.AreEqual(30, servers.get_ItemByDNSHost("surbl.example.test").ID);
        Assert.IsNull(servers.get_ItemByDNSHost("multi.surbl.org"));
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = servers.get_ItemByDBID(10)).ErrorCode);
    }

    private static SurblServerAdministrationSnapshot Snapshot(
        int id,
        bool active,
        string dnsHost,
        string rejectMessage,
        int score) =>
        new(id, active, dnsHost, rejectMessage, score);

    private static void AssertServer(
        IInterfaceSURBLServer server,
        int id,
        bool active,
        string dnsHost,
        string rejectMessage,
        int score)
    {
        Assert.AreEqual(id, server.ID);
        Assert.AreEqual(active, server.Active);
        Assert.AreEqual(dnsHost, server.DNSHost);
        Assert.AreEqual(rejectMessage, server.RejectMessage);
        Assert.AreEqual(score, server.Score);
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

    private static void AssertVariantBoolProperty(Type contract, string name, int dispatchId)
    {
        var property = GetProperty(contract, name);

        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    private static void AssertBstrProperty(Type contract, string name, int dispatchId)
    {
        var property = GetProperty(contract, name);

        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.BStr, property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.BStr, property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    private static PropertyInfo GetProperty(Type contract, string name) =>
        contract.GetProperty(name) ?? throw new AssertFailedException($"Missing property {name}.");

    private static MethodInfo GetMethod(Type contract, string name) =>
        contract.GetMethod(name) ?? throw new AssertFailedException($"Missing method {name}.");

    private static void AssertPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);
        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }

    private sealed class MutableSurblServerAdministrationStore(
        IReadOnlyList<SurblServerAdministrationSnapshot> servers)
        : ISurblServerAdministrationStore
    {
        private IReadOnlyList<SurblServerAdministrationSnapshot> _servers = servers;

        public int ReadCount { get; private set; }

        public void Replace(IReadOnlyList<SurblServerAdministrationSnapshot> servers)
        {
            _servers = servers;
        }

        public ValueTask<IReadOnlyList<SurblServerAdministrationSnapshot>> GetSurblServersAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<SurblServerAdministrationSnapshot>>(
                _servers.OrderBy(static server => server.Id).ToArray());
        }
    }
}
