using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WhiteListAddressesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceWhiteListAddresses),
            "8492EE2E-7332-4253-B93E-D8B011B47D78",
            new[]
            {
                "get_Item",
                "get_Count",
                "DeleteByDBID",
                "Add",
                "get_ItemByDBID",
                "Refresh",
                "Clear"
            });
        Assert.AreEqual(0, GetProperty(typeof(IInterfaceWhiteListAddresses), "Item").GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(5, GetMethod(typeof(IInterfaceWhiteListAddresses), "get_ItemByDBID").GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(7, GetMethod(typeof(IInterfaceWhiteListAddresses), nameof(IInterfaceWhiteListAddresses.Clear)).GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.IsNull(typeof(IInterfaceWhiteListAddresses).GetMethod("get_ItemByName"));

        AssertContract(
            typeof(IInterfaceWhiteListAddress),
            "D67457A7-3500-481F-900F-C9741C89D6AB",
            new[]
            {
                "get_ID",
                "get_LowerIPAddress",
                "set_LowerIPAddress",
                "get_UpperIPAddress",
                "set_UpperIPAddress",
                "get_EmailAddress",
                "set_EmailAddress",
                "get_Description",
                "set_Description",
                "Save",
                "Delete"
            });
        AssertBstrProperty(typeof(IInterfaceWhiteListAddress), nameof(IInterfaceWhiteListAddress.LowerIPAddress), 2);
        AssertBstrProperty(typeof(IInterfaceWhiteListAddress), nameof(IInterfaceWhiteListAddress.UpperIPAddress), 3);
        AssertBstrProperty(typeof(IInterfaceWhiteListAddress), nameof(IInterfaceWhiteListAddress.EmailAddress), 4);
        AssertBstrProperty(typeof(IInterfaceWhiteListAddress), nameof(IInterfaceWhiteListAddress.Description), 5);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<WhiteListAddresses>(
            "FACFAF38-7BEE-48B4-A47E-D623ACCAE9AB",
            "hMailServer.WhiteListAddresses.1",
            typeof(IInterfaceWhiteListAddresses));
        AssertComClass<WhiteListAddress>(
            "0B18E4F3-4423-403E-B275-1D95CBD353CE",
            "hMailServer.WhiteListAddress.1",
            typeof(IInterfaceWhiteListAddress));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var collectionError = Assert.ThrowsExactly<COMException>(() => _ = new WhiteListAddresses().Count);
        var collectionRefreshError = Assert.ThrowsExactly<COMException>(new WhiteListAddresses().Refresh);
        var itemError = Assert.ThrowsExactly<COMException>(() => _ = new WhiteListAddress().LowerIPAddress);
        var antiSpamError = Assert.ThrowsExactly<COMException>(() => _ = new AntiSpam().WhiteListAddresses);

        Assert.AreEqual(EAccessDenied, collectionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, collectionRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, itemError.ErrorCode);
        Assert.AreEqual(EAccessDenied, antiSpamError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupResults()
    {
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "*@example.test", "Test network"),
                Snapshot(20, "203.0.113.5", "203.0.113.5", "sender@example.test", "Single address")
            });

        Assert.AreEqual(2, addresses.Count);
        AssertAddress(addresses[0], 10, "192.0.2.1", "192.0.2.255", "*@example.test", "Test network");
        AssertAddress(
            addresses.get_ItemByDBID(20),
            20,
            "203.0.113.5",
            "203.0.113.5",
            "sender@example.test",
            "Single address");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = addresses[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByDBID(30));
        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);

        AssertPending(() => addresses.Add());
        AssertPending(() => addresses.DeleteByDBID(10));
        AssertPending(addresses.Clear);
        AssertPending(() => addresses[0].LowerIPAddress = "198.51.100.1");
        AssertPending(() => addresses[0].UpperIPAddress = "198.51.100.255");
        AssertPending(() => addresses[0].EmailAddress = "changed@example.test");
        AssertPending(() => addresses[0].Description = "Changed");
        AssertPending(addresses[0].Save);
        AssertPending(addresses[0].Delete);
    }

    [TestMethod]
    public void AuthorizedCollection_AddStagesNewAddressAndAppendsOnlyAfterInsert()
    {
        WhiteListAddressAdministrationSnapshot? inserted = null;
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "existing@example.test", "Existing")
            },
            insert: address =>
            {
                inserted = address;
                return 30;
            },
            isServerAdministrator: static () => true);

        var added = addresses.Add();

        Assert.AreEqual(0, added.ID);
        Assert.AreEqual("0.0.0.0", added.LowerIPAddress);
        Assert.AreEqual("0.0.0.0", added.UpperIPAddress);
        Assert.AreEqual(string.Empty, added.EmailAddress);
        Assert.AreEqual(string.Empty, added.Description);

        added.LowerIPAddress = "198.51.100.1";
        added.LowerIPAddress = "invalid-ip";
        added.UpperIPAddress = "198.51.100.255";
        added.UpperIPAddress = "300.300.300.300";
        added.EmailAddress = "sender@example.test";
        added.Description = "New address";

        Assert.AreEqual("198.51.100.1", added.LowerIPAddress);
        Assert.AreEqual("198.51.100.255", added.UpperIPAddress);

        added.Save();

        Assert.IsNotNull(inserted);
        Assert.AreEqual(0, inserted!.Id);
        Assert.AreEqual("198.51.100.1", inserted.LowerIpAddress);
        Assert.AreEqual("198.51.100.255", inserted.UpperIpAddress);
        Assert.AreEqual("sender@example.test", inserted.EmailAddress);
        Assert.AreEqual("New address", inserted.Description);
        Assert.AreEqual(30, added.ID);
        Assert.AreEqual(2, addresses.Count);
        Assert.AreEqual(30, addresses.get_ItemByDBID(30).ID);
    }

    [TestMethod]
    public void AuthorizedCollection_NewAddressSaveMapsFailureAndRetainsUnsavedFacade()
    {
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "existing@example.test", "Existing")
            },
            insert: static _ => throw new InvalidOperationException("Simulated insert failure."),
            isServerAdministrator: static () => true);
        var added = addresses.Add();
        added.EmailAddress = "sender@example.test";

        var error = Assert.ThrowsExactly<COMException>(added.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(0, added.ID);
        Assert.AreEqual(1, addresses.Count);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByDBID(0)).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_NewAddressSaveRechecksServerAdministrator()
    {
        var isServerAdministrator = true;
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            Array.Empty<WhiteListAddressAdministrationSnapshot>(),
            insert: static _ => 30,
            isServerAdministrator: () => isServerAdministrator);
        var added = addresses.Add();
        isServerAdministrator = false;

        var addError = Assert.ThrowsExactly<COMException>(() => addresses.Add());
        var error = Assert.ThrowsExactly<COMException>(added.Save);

        Assert.AreEqual(EAccessDenied, addError.ErrorCode);
        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, added.ID);
        Assert.AreEqual(0, addresses.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_ExistingAddressSaveUpdatesOnlyAfterSuccessfulStore()
    {
        WhiteListAddressAdministrationSnapshot? updated = null;
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "existing@example.test", "Existing")
            },
            update: address => updated = address,
            isServerAdministrator: static () => true);

        var existing = addresses[0];
        existing.LowerIPAddress = "198.51.100.1";
        existing.LowerIPAddress = "invalid-ip";
        existing.UpperIPAddress = "198.51.100.255";
        existing.EmailAddress = "updated@example.test";
        existing.Description = "Updated";

        existing.Save();

        Assert.IsNotNull(updated);
        Assert.AreEqual(10, updated!.Id);
        Assert.AreEqual("198.51.100.1", updated.LowerIpAddress);
        Assert.AreEqual("198.51.100.255", updated.UpperIpAddress);
        Assert.AreEqual("updated@example.test", updated.EmailAddress);
        Assert.AreEqual("Updated", updated.Description);
        AssertAddress(
            addresses.get_ItemByDBID(10),
            10,
            "198.51.100.1",
            "198.51.100.255",
            "updated@example.test",
            "Updated");
    }

    [TestMethod]
    public void AuthorizedCollection_ExistingAddressSaveMapsFailureAndRetainsSnapshots()
    {
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "existing@example.test", "Existing")
            },
            update: static _ => throw new InvalidOperationException("Simulated update failure."),
            isServerAdministrator: static () => true);
        var existing = addresses[0];
        existing.Description = "Staged update";

        var error = Assert.ThrowsExactly<COMException>(existing.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual("Staged update", existing.Description);
        Assert.AreEqual("Existing", addresses.get_ItemByDBID(10).Description);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteUsesOwningScopeAndRemovesAfterStoreSuccess()
    {
        var deletedId = 0L;
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "first@example.test", "First"),
                Snapshot(20, "203.0.113.1", "203.0.113.1", "second@example.test", "Second")
            },
            delete: id =>
            {
                deletedId = id;
                return true;
            },
            isServerAdministrator: static () => true);

        addresses.DeleteByDBID(10);

        Assert.AreEqual(10, deletedId);
        Assert.AreEqual(1, addresses.Count);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByDBID(10)).ErrorCode);

        addresses.DeleteByDBID(999);
        var retained = addresses.get_ItemByDBID(20);
        retained.Delete();
        retained.Delete();

        Assert.AreEqual(20, deletedId);
        Assert.AreEqual(0, addresses.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteFailureMapsToFailureAndRetainsSnapshot()
    {
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "first@example.test", "First")
            },
            delete: static _ => false,
            isServerAdministrator: static () => true);

        var error = Assert.ThrowsExactly<COMException>(() => addresses.DeleteByDBID(10));

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(1, addresses.Count);
        Assert.AreEqual(10, addresses.get_ItemByDBID(10).ID);
    }

    [TestMethod]
    public void AuthorizedCollection_ClearRemovesAllAddressesAfterStoreSuccess()
    {
        var clearCalls = 0;
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "first@example.test", "First"),
                Snapshot(20, "203.0.113.1", "203.0.113.1", "second@example.test", "Second")
            },
            clear: () => clearCalls++,
            isServerAdministrator: static () => true);

        addresses.Clear();

        Assert.AreEqual(1, clearCalls);
        Assert.AreEqual(0, addresses.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_ClearFailureRetainsSnapshot()
    {
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "first@example.test", "First")
            },
            clear: static () => throw new InvalidOperationException("Simulated clear failure."),
            isServerAdministrator: static () => true);

        var error = Assert.ThrowsExactly<COMException>(addresses.Clear);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(1, addresses.Count);
        Assert.AreEqual(10, addresses[0].ID);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "*@example.test", "Test network")
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
                    Snapshot(30, "198.51.100.1", "198.51.100.255", "refreshed@example.test", "Refreshed network"),
                    Snapshot(20, "203.0.113.5", "203.0.113.5", "sender@example.test", "Single address")
                };
            });

        Assert.AreEqual(1, addresses.Count);
        Assert.AreEqual("192.0.2.1", addresses[0].LowerIPAddress);

        addresses.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, addresses.Count);
        AssertAddress(
            addresses[0],
            30,
            "198.51.100.1",
            "198.51.100.255",
            "refreshed@example.test",
            "Refreshed network");
        Assert.AreEqual(20, addresses.get_ItemByDBID(20).ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(addresses.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, addresses.Count);
        Assert.AreEqual("Refreshed network", addresses.get_ItemByDBID(30).Description);
    }

    [TestMethod]
    public void Item_PreservesLegacyBigintIdProjection()
    {
        var address = WhiteListAddress.CreateAuthorized(
            Snapshot(0x1_0000_0005, "192.0.2.1", "192.0.2.1", string.Empty, string.Empty));

        Assert.AreEqual(5, address.ID);
    }

    [TestMethod]
    public void AuthorizedAntiSpam_UsesConfiguredWhiteListAddressRuntime()
    {
        var store = new MutableWhiteListAddressAdministrationStore(
            new[]
            {
                Snapshot(20, "203.0.113.5", "203.0.113.5", "sender@example.test", "Single address"),
                Snapshot(10, "192.0.2.1", "192.0.2.255", "*@example.test", "Test network")
            });
        WhiteListAddressAdministrationRuntimeHost.Configure(
            store);
        var antiSpam = AntiSpam.CreateAuthorized(new AntiSpamAdministrationSnapshot());

        var addresses = antiSpam.WhiteListAddresses;

        Assert.AreEqual(2, addresses.Count);
        Assert.AreEqual(10, addresses[0].ID);
        Assert.AreEqual("Single address", addresses.get_ItemByDBID(20).Description);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(20, "203.0.113.5", "203.0.113.5", "sender@example.test", "Single address"),
                Snapshot(30, "198.51.100.1", "198.51.100.255", "refreshed@example.test", "Refreshed network")
            });

        addresses.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, addresses.Count);
        AssertAddress(
            addresses[0],
            30,
            "198.51.100.1",
            "198.51.100.255",
            "refreshed@example.test",
            "Refreshed network");
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByDBID(10)).ErrorCode);
    }

    [TestMethod]
    public void FailedReauthentication_DeniesNewAntiSpamWhiteListAccessButRetainedObjectsRemainReadable()
    {
        var settingsStore = new RecordingSettingsAdministrationStore(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty));
        var whiteListStore = new MutableWhiteListAddressAdministrationStore(
            new[]
            {
                Snapshot(10, "192.0.2.1", "192.0.2.255", "*@example.test", "Test network")
            });
        SettingsAdministrationRuntimeHost.Configure(settingsStore);
        WhiteListAddressAdministrationRuntimeHost.Configure(whiteListStore);
        var application = Application.CreateForRuntime(
            new TestAdministratorAuthenticationProvider("secret"));

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;
        var retainedAntiSpam = settings.AntiSpam;
        var retainedAddresses = retainedAntiSpam.WhiteListAddresses;
        var retainedAddress = retainedAddresses[0];

        Assert.AreEqual(2, settingsStore.ReadCount);
        Assert.AreEqual(1, whiteListStore.ReadCount);

        Assert.IsNull(application.Authenticate("Administrator", "wrong"));

        var newlyObtainedAntiSpam = settings.AntiSpam;
        var deniedError = Assert.ThrowsExactly<COMException>(
            () => _ = newlyObtainedAntiSpam.WhiteListAddresses);

        Assert.AreEqual(EAccessDenied, deniedError.ErrorCode);
        Assert.AreEqual(2, settingsStore.ReadCount);
        Assert.AreEqual(1, whiteListStore.ReadCount);

        var postReauthenticationAddresses = retainedAntiSpam.WhiteListAddresses;

        Assert.AreEqual(2, settingsStore.ReadCount);
        Assert.AreEqual(2, whiteListStore.ReadCount);
        Assert.AreEqual(1, postReauthenticationAddresses.Count);

        Assert.AreEqual(1, retainedAddresses.Count);
        Assert.AreEqual(10, retainedAddresses.get_ItemByDBID(10).ID);
        AssertAddress(
            retainedAddress,
            10,
            "192.0.2.1",
            "192.0.2.255",
            "*@example.test",
            "Test network");
        Assert.AreEqual(2, settingsStore.ReadCount);
        Assert.AreEqual(2, whiteListStore.ReadCount);
    }

    [TestMethod]
    public void CollectionMutations_HoldAuthorizationLeaseAcrossStoreCallbacks()
    {
        var activeLeases = 0;
        var disposedLeases = 0;
        var leaseFactory = new Func<CancellationToken, ValueTask<IDisposable?>>(_ =>
        {
            activeLeases++;
            return ValueTask.FromResult<IDisposable?>(new TrackingLease(() =>
            {
                activeLeases--;
                disposedLeases++;
            }));
        });

        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[] { Snapshot(10, "192.0.2.1", "192.0.2.1", "first@example.test", "First") },
            insert: _ =>
            {
                Assert.AreEqual(1, activeLeases);
                return 20;
            },
            update: _ => Assert.AreEqual(1, activeLeases),
            delete: _ =>
            {
                Assert.AreEqual(1, activeLeases);
                return true;
            },
            clear: () => Assert.AreEqual(1, activeLeases),
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: leaseFactory);

        var draft = addresses.Add();
        draft.Save();
        var existing = addresses.get_ItemByDBID(10);
        existing.Description = "Updated";
        existing.Save();
        existing.Delete();
        addresses.Clear();

        Assert.AreEqual(0, activeLeases);
        Assert.AreEqual(4, disposedLeases);
    }

    [TestMethod]
    public void CollectionMutations_DenyBeforeStoreWhenAuthorizationLeaseIsUnavailable()
    {
        var stores = 0;
        IInterfaceWhiteListAddresses addresses = WhiteListAddresses.CreateAuthorized(
            new[] { Snapshot(10, "192.0.2.1", "192.0.2.1", "first@example.test", "First") },
            insert: _ =>
            {
                stores++;
                return 20;
            },
            update: _ => stores++,
            delete: _ =>
            {
                stores++;
                return true;
            },
            clear: () => stores++,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: _ => ValueTask.FromResult<IDisposable?>(null));

        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(() => addresses.Add().Save()).ErrorCode);
        var existing = addresses.get_ItemByDBID(10);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(() => existing.Save()).ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(() => existing.Delete()).ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(addresses.Clear).ErrorCode);
        Assert.AreEqual(0, stores);
    }

    private static WhiteListAddressAdministrationSnapshot Snapshot(
        long id,
        string lowerIpAddress,
        string upperIpAddress,
        string emailAddress,
        string description) =>
        new(id, lowerIpAddress, upperIpAddress, emailAddress, description);

    private static void AssertAddress(
        IInterfaceWhiteListAddress address,
        int id,
        string lowerIpAddress,
        string upperIpAddress,
        string emailAddress,
        string description)
    {
        Assert.AreEqual(id, address.ID);
        Assert.AreEqual(lowerIpAddress, address.LowerIPAddress);
        Assert.AreEqual(upperIpAddress, address.UpperIPAddress);
        Assert.AreEqual(emailAddress, address.EmailAddress);
        Assert.AreEqual(description, address.Description);
    }

    private sealed class TrackingLease(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
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

    private sealed class MutableWhiteListAddressAdministrationStore(
        IReadOnlyList<WhiteListAddressAdministrationSnapshot> addresses)
        : IWhiteListAddressAdministrationStore
    {
        private IReadOnlyList<WhiteListAddressAdministrationSnapshot> _addresses = addresses;

        public int ReadCount { get; private set; }

        public void Replace(IReadOnlyList<WhiteListAddressAdministrationSnapshot> addresses)
        {
            _addresses = addresses;
        }

        public ValueTask<IReadOnlyList<WhiteListAddressAdministrationSnapshot>> GetWhiteListAddressesAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<WhiteListAddressAdministrationSnapshot>>(
                _addresses.OrderBy(static address => System.Net.IPAddress.Parse(address.LowerIpAddress).GetAddressBytes(),
                    ByteArrayComparer.Instance).ToArray());
        }

        public ValueTask<long> InsertWhiteListAddressAsync(
            WhiteListAddressAdministrationSnapshot address,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Insert is outside this read-only test fixture.");

        public ValueTask UpdateWhiteListAddressAsync(
            WhiteListAddressAdministrationSnapshot address,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Update is outside this read-only test fixture.");

        public ValueTask<bool> DeleteWhiteListAddressByIdAsync(
            long databaseId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Delete is outside this read-only test fixture.");

        public ValueTask ClearWhiteListAddressesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("Clear is outside this read-only test fixture.");
    }

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public int Compare(byte[]? left, byte[]? right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);

            for (var index = 0; index < Math.Min(left.Length, right.Length); index++)
            {
                var comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return left.Length.CompareTo(right.Length);
        }
    }

    private sealed class RecordingSettingsAdministrationStore(SettingsAdministrationSnapshot snapshot)
        : ISettingsAdministrationStore
    {
        public int ReadCount { get; private set; }

        public ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult(snapshot);
        }
    }

    private sealed class TestAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            string.Equals(username, "Administrator", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(attemptedPassword, password, StringComparison.Ordinal);
    }
}
