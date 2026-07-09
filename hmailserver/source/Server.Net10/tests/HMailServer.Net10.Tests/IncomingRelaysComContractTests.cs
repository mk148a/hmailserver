using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class IncomingRelaysComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceIncomingRelays),
            "49D48933-3219-4D7E-84D5-B26FE5F0E165",
            new[]
            {
                "get_Item", "get_ItemByDBID", "Delete", "DeleteByDBID", "Refresh",
                "Add", "get_Count", "get_ItemByName"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceIncomingRelays).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            7,
            typeof(IInterfaceIncomingRelays).GetMethod("get_ItemByName")?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceIncomingRelay),
            "088D748B-7CCE-4B8D-A103-D99DA83775AB",
            new[]
            {
                "get_ID", "get_LowerIP", "set_LowerIP", "get_UpperIP", "set_UpperIP",
                "get_Name", "set_Name", "Delete", "Save"
            });
        Assert.AreEqual(
            5,
            typeof(IInterfaceIncomingRelay).GetMethod(nameof(IInterfaceIncomingRelay.Save))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<IncomingRelays>(
            "3E75EE53-EAA6-40A5-B2CE-9CB8D7EE9278",
            "hMailServer.IncomingRelays.1",
            typeof(IInterfaceIncomingRelays));
        AssertComClass<IncomingRelay>(
            "CB3F5F58-436C-4358-8E1C-1BE1F6D822BC",
            "hMailServer.IncomingRelay.1",
            typeof(IInterfaceIncomingRelay));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var relaysError = Assert.ThrowsExactly<COMException>(() => _ = new IncomingRelays().Count);
        var relaysDeleteError = Assert.ThrowsExactly<COMException>(() => new IncomingRelays().Delete(0));
        var relaysDeleteByIdError = Assert.ThrowsExactly<COMException>(() => new IncomingRelays().DeleteByDBID(10));
        var relaysRefreshError = Assert.ThrowsExactly<COMException>(new IncomingRelays().Refresh);
        var relayError = Assert.ThrowsExactly<COMException>(() => _ = new IncomingRelay().Name);
        var relayDeleteError = Assert.ThrowsExactly<COMException>(new IncomingRelay().Delete);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().IncomingRelays);

        Assert.AreEqual(EAccessDenied, relaysError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relaysDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relaysDeleteByIdError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relaysRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        IInterfaceIncomingRelays relays = IncomingRelays.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Alpha relay", "127.0.0.1", "127.0.0.1"),
                Snapshot(20, "Beta relay", "10.0.0.0", "10.0.0.255")
            });

        Assert.AreEqual(2, relays.Count);
        AssertRelay(relays[0], 10, "Alpha relay", "127.0.0.1", "127.0.0.1");
        AssertRelay(relays.get_ItemByName("BETA RELAY"), 20, "Beta relay", "10.0.0.0", "10.0.0.255");
        Assert.AreEqual(20, relays.get_ItemByDBID(20).ID);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = relays[2]);
        var badName = Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByName("missing"));
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByDBID(30));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => relays.Add());
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => relays.Delete(0));
        var pendingDeleteById = Assert.ThrowsExactly<COMException>(() => relays.DeleteByDBID(10));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(relays.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => relays[0].Name = "changed");
        var pendingSave = Assert.ThrowsExactly<COMException>(relays[0].Save);
        var pendingRelayDelete = Assert.ThrowsExactly<COMException>(relays[0].Delete);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDeleteById.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRelayDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByDBIDCallsConfiguredOperationAndRetainsSnapshotOnFailure()
    {
        var failDelete = true;
        var deletedIds = new List<int>();
        IInterfaceIncomingRelays relays = IncomingRelays.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Alpha relay", "127.0.0.1", "127.0.0.1"),
                Snapshot(20, "Beta relay", "10.0.0.0", "10.0.0.255")
            },
            deleteById: databaseId =>
            {
                deletedIds.Add(databaseId);
                if (failDelete)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }
            });

        var deleteFailure = Assert.ThrowsExactly<COMException>(() => relays.DeleteByDBID(10));

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(new[] { 10 }, deletedIds);
        Assert.AreEqual(2, relays.Count);
        AssertRelay(relays[0], 10, "Alpha relay", "127.0.0.1", "127.0.0.1");

        failDelete = false;
        relays.DeleteByDBID(10);

        CollectionAssert.AreEqual(new[] { 10, 10 }, deletedIds);
        Assert.AreEqual(1, relays.Count);
        AssertRelay(relays[0], 20, "Beta relay", "10.0.0.0", "10.0.0.255");
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByDBID(10)).ErrorCode);

        relays.DeleteByDBID(999);

        CollectionAssert.AreEqual(new[] { 10, 10, 999 }, deletedIds);
        Assert.AreEqual(1, relays.Count);
        AssertRelay(relays[0], 20, "Beta relay", "10.0.0.0", "10.0.0.255");
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByIndexCallsConfiguredOperationAndRetainsSnapshotOnFailure()
    {
        var failDelete = true;
        var deletedIds = new List<int>();
        IInterfaceIncomingRelays relays = IncomingRelays.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Alpha relay", "127.0.0.1", "127.0.0.1"),
                Snapshot(20, "Beta relay", "10.0.0.0", "10.0.0.255")
            },
            deleteById: databaseId =>
            {
                deletedIds.Add(databaseId);
                if (failDelete)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }
            });

        var deleteFailure = Assert.ThrowsExactly<COMException>(() => relays.Delete(0));

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(new[] { 10 }, deletedIds);
        Assert.AreEqual(2, relays.Count);
        AssertRelay(relays[0], 10, "Alpha relay", "127.0.0.1", "127.0.0.1");

        failDelete = false;
        relays.Delete(0);

        CollectionAssert.AreEqual(new[] { 10, 10 }, deletedIds);
        Assert.AreEqual(1, relays.Count);
        AssertRelay(relays[0], 20, "Beta relay", "10.0.0.0", "10.0.0.255");
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByDBID(10)).ErrorCode);

        var badIndex = Assert.ThrowsExactly<COMException>(() => relays.Delete(5));

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        CollectionAssert.AreEqual(new[] { 10, 10 }, deletedIds);

        relays.Delete(0);

        CollectionAssert.AreEqual(new[] { 10, 10, 20 }, deletedIds);
        Assert.AreEqual(0, relays.Count);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByDBID(20)).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ItemDeleteCallsConfiguredOperationAndUpdatesOwningSnapshot()
    {
        var failDelete = true;
        var deletedIds = new List<int>();
        IInterfaceIncomingRelays relays = IncomingRelays.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Alpha relay", "127.0.0.1", "127.0.0.1"),
                Snapshot(20, "Beta relay", "10.0.0.0", "10.0.0.255")
            },
            deleteById: databaseId =>
            {
                deletedIds.Add(databaseId);
                if (failDelete)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }
            });
        var alpha = relays[0];
        var beta = relays.get_ItemByDBID(20);

        var deleteFailure = Assert.ThrowsExactly<COMException>(alpha.Delete);

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(new[] { 10 }, deletedIds);
        Assert.AreEqual(2, relays.Count);
        AssertRelay(relays[0], 10, "Alpha relay", "127.0.0.1", "127.0.0.1");

        failDelete = false;
        alpha.Delete();

        CollectionAssert.AreEqual(new[] { 10, 10 }, deletedIds);
        Assert.AreEqual(1, relays.Count);
        AssertRelay(relays[0], 20, "Beta relay", "10.0.0.0", "10.0.0.255");
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByDBID(10)).ErrorCode);

        beta.Delete();

        CollectionAssert.AreEqual(new[] { 10, 10, 20 }, deletedIds);
        Assert.AreEqual(0, relays.Count);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByDBID(20)).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceIncomingRelays relays = IncomingRelays.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Alpha relay", "127.0.0.1", "127.0.0.1")
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
                    Snapshot(20, "Beta relay", "10.0.0.0", "10.0.0.255"),
                    Snapshot(30, "Gamma relay", "192.168.1.1", "192.168.1.254")
                };
            });

        Assert.AreEqual(1, relays.Count);
        Assert.AreEqual("Alpha relay", relays[0].Name);

        relays.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, relays.Count);
        AssertRelay(relays[0], 20, "Beta relay", "10.0.0.0", "10.0.0.255");
        Assert.AreEqual(30, relays.get_ItemByName("GAMMA RELAY").ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(relays.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, relays.Count);
        Assert.AreEqual("Beta relay", relays.get_ItemByDBID(20).Name);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredIncomingRelayRuntime()
    {
        var store = new MutableIncomingRelayAdministrationStore(
            new[]
            {
                Snapshot(20, "Beta relay", "10.0.0.0", "10.0.0.255"),
                Snapshot(10, "Alpha relay", "127.0.0.1", "127.0.0.1")
            });
        IncomingRelayAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var relays = settings.IncomingRelays;

        Assert.AreEqual(2, relays.Count);
        Assert.AreEqual("Alpha relay", relays[0].Name);
        Assert.AreEqual(1, store.ReadCount);

        relays.DeleteByDBID(20);

        Assert.AreEqual(1, store.DeletedIds.Count);
        Assert.AreEqual(20, store.DeletedIds[0]);
        Assert.AreEqual(1, relays.Count);
        AssertRelay(relays[0], 10, "Alpha relay", "127.0.0.1", "127.0.0.1");

        store.Replace(
            new[]
            {
                Snapshot(30, "Gamma relay", "192.168.1.1", "192.168.1.254"),
                Snapshot(20, "Beta relay", "10.0.0.0", "10.0.0.255")
            });

        relays.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, relays.Count);
        Assert.AreEqual("Beta relay", relays[0].Name);
        Assert.AreEqual(30, relays.get_ItemByDBID(30).ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByDBID(10)).ErrorCode);

        relays.get_ItemByDBID(30).Delete();

        Assert.AreEqual(2, store.DeletedIds.Count);
        Assert.AreEqual(30, store.DeletedIds[1]);
        Assert.AreEqual(1, relays.Count);
        AssertRelay(relays[0], 20, "Beta relay", "10.0.0.0", "10.0.0.255");
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByDBID(30)).ErrorCode);

        relays.Delete(0);

        Assert.AreEqual(3, store.DeletedIds.Count);
        Assert.AreEqual(20, store.DeletedIds[2]);
        Assert.AreEqual(0, relays.Count);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = relays.get_ItemByDBID(20)).ErrorCode);
    }

    private static IncomingRelayAdministrationSnapshot Snapshot(
        int id,
        string name,
        string lowerIp,
        string upperIp) =>
        new(id, name, lowerIp, upperIp);

    private static void AssertRelay(
        IInterfaceIncomingRelay relay,
        int id,
        string name,
        string lowerIp,
        string upperIp)
    {
        Assert.AreEqual(id, relay.ID);
        Assert.AreEqual(name, relay.Name);
        Assert.AreEqual(lowerIp, relay.LowerIP);
        Assert.AreEqual(upperIp, relay.UpperIP);
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

    private sealed class MutableIncomingRelayAdministrationStore(IReadOnlyList<IncomingRelayAdministrationSnapshot> relays)
        : IIncomingRelayAdministrationStore
    {
        private IReadOnlyList<IncomingRelayAdministrationSnapshot> _relays = relays;

        public int ReadCount { get; private set; }

        public List<int> DeletedIds { get; } = [];

        public void Replace(IReadOnlyList<IncomingRelayAdministrationSnapshot> relays)
        {
            _relays = relays;
        }

        public ValueTask<IReadOnlyList<IncomingRelayAdministrationSnapshot>> GetIncomingRelaysAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<IncomingRelayAdministrationSnapshot>>(
                _relays.OrderBy(relay => relay.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }

        public ValueTask DeleteIncomingRelayByIdAsync(
            int databaseId,
            CancellationToken cancellationToken)
        {
            DeletedIds.Add(databaseId);
            _relays = _relays
                .Where(relay => relay.Id != databaseId)
                .ToArray();
            return ValueTask.CompletedTask;
        }
    }
}
