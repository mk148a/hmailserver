using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class GreyListingWhiteAddressesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceGreyListingWhiteAddresses),
            "D8D54486-4CC5-4240-A4BF-DD68D9C3E85B",
            new[]
            {
                "get_Item",
                "get_Count",
                "DeleteByDBID",
                "Add",
                "get_ItemByDBID",
                "Refresh",
                "get_ItemByName"
            });
        Assert.AreEqual(
            0,
            GetProperty(typeof(IInterfaceGreyListingWhiteAddresses), "Item")
                .GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            5,
            GetMethod(typeof(IInterfaceGreyListingWhiteAddresses), "get_ItemByDBID")
                .GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            7,
            GetMethod(typeof(IInterfaceGreyListingWhiteAddresses), "get_ItemByName")
                .GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.BStr,
            GetMethod(typeof(IInterfaceGreyListingWhiteAddresses), "get_ItemByName")
                .GetParameters()[0]
                .GetCustomAttribute<MarshalAsAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceGreyListingWhiteAddress),
            "A32DF62B-043F-4C0D-81E9-F4CC3CB62F33",
            new[]
            {
                "get_ID",
                "get_IPAddress",
                "set_IPAddress",
                "get_Description",
                "set_Description",
                "Save",
                "Delete"
            });
        AssertBstrProperty(
            typeof(IInterfaceGreyListingWhiteAddress),
            nameof(IInterfaceGreyListingWhiteAddress.IPAddress),
            2);
        AssertBstrProperty(
            typeof(IInterfaceGreyListingWhiteAddress),
            nameof(IInterfaceGreyListingWhiteAddress.Description),
            3);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<GreyListingWhiteAddresses>(
            "F8BB11B8-5DD1-438E-AF29-6E088AA0BD06",
            "hMailServer.GreyListingWhiteAddresses.1",
            typeof(IInterfaceGreyListingWhiteAddresses));
        AssertComClass<GreyListingWhiteAddress>(
            "771EDD01-0E62-4071-AE72-88E439EC0880",
            "hMailServer.GreyListingWhiteAddress.1",
            typeof(IInterfaceGreyListingWhiteAddress));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var collectionError = Assert.ThrowsExactly<COMException>(() => _ = new GreyListingWhiteAddresses().Count);
        var collectionRefreshError = Assert.ThrowsExactly<COMException>(new GreyListingWhiteAddresses().Refresh);
        var itemError = Assert.ThrowsExactly<COMException>(() => _ = new GreyListingWhiteAddress().IPAddress);
        var antiSpamError = Assert.ThrowsExactly<COMException>(() => _ = new AntiSpam().GreyListingWhiteAddresses);

        Assert.AreEqual(EAccessDenied, collectionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, collectionRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, itemError.ErrorCode);
        Assert.AreEqual(EAccessDenied, antiSpamError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupResults()
    {
        IInterfaceGreyListingWhiteAddresses addresses = GreyListingWhiteAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.%", "Test network"),
                Snapshot(20, "203.0.113.5", "Single address")
            });

        Assert.AreEqual(2, addresses.Count);
        AssertAddress(addresses[0], 10, "192.0.2.*", "Test network");
        AssertAddress(addresses.get_ItemByDBID(20), 20, "203.0.113.5", "Single address");
        Assert.AreEqual(10, addresses.get_ItemByName("192.0.2.%").ID);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = addresses[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByDBID(30));
        var badStoredName = Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByName("192.0.2.*"));
        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badStoredName.ErrorCode);

        AssertPending(() => addresses.Add());
        AssertPending(() => addresses.DeleteByDBID(10));
        AssertPending(addresses.Refresh);
        AssertPending(() => addresses[0].IPAddress = "198.51.100.*");
        AssertPending(() => addresses[0].Description = "Changed");
        AssertPending(addresses[0].Save);
        AssertPending(addresses[0].Delete);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceGreyListingWhiteAddresses addresses = GreyListingWhiteAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(10, "192.0.2.%", "Test network")
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
                    Snapshot(30, "198.51.100.%", "Refreshed network"),
                    Snapshot(20, "203.0.113.5", "Single address")
                };
            });

        Assert.AreEqual(1, addresses.Count);
        Assert.AreEqual("192.0.2.*", addresses[0].IPAddress);

        addresses.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, addresses.Count);
        AssertAddress(addresses[0], 30, "198.51.100.*", "Refreshed network");
        Assert.AreEqual(20, addresses.get_ItemByDBID(20).ID);
        Assert.AreEqual(30, addresses.get_ItemByName("198.51.100.%").ID);
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
    public void Item_PreservesLegacyLikePatternAndBigintIdProjections()
    {
        var address = GreyListingWhiteAddress.CreateAuthorized(
            Snapshot(0x1_0000_0005, "10.0._.%", "Pattern"));

        Assert.AreEqual(5, address.ID);
        Assert.AreEqual("10.0.?.*", address.IPAddress);
        Assert.AreEqual("literal%_/", GreyListingWhiteAddress.ConvertLikeToWildcard("literal/%/_//"));
    }

    [TestMethod]
    public void AuthorizedAntiSpam_UsesConfiguredGreyListingWhiteAddressRuntime()
    {
        var store = new MutableGreyListingWhiteAddressAdministrationStore(
            new[]
            {
                Snapshot(20, "203.0.113.5", "Single address"),
                Snapshot(10, "192.0.2.%", "Test network")
            });
        GreyListingWhiteAddressAdministrationRuntimeHost.Configure(
            store);
        var antiSpam = AntiSpam.CreateAuthorized(new AntiSpamAdministrationSnapshot());

        var addresses = antiSpam.GreyListingWhiteAddresses;

        Assert.AreEqual(2, addresses.Count);
        Assert.AreEqual(10, addresses[0].ID);
        Assert.AreEqual("Single address", addresses.get_ItemByDBID(20).Description);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(20, "203.0.113.5", "Single address"),
                Snapshot(30, "198.51.100.%", "Refreshed network")
            });

        addresses.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, addresses.Count);
        AssertAddress(addresses[0], 30, "198.51.100.*", "Refreshed network");
        Assert.AreEqual(30, addresses.get_ItemByName("198.51.100.%").ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByDBID(10)).ErrorCode);
    }

    private static GreyListingWhiteAddressAdministrationSnapshot Snapshot(
        long id,
        string storedIpAddress,
        string description) =>
        new(id, storedIpAddress, description);

    private static void AssertAddress(
        IInterfaceGreyListingWhiteAddress address,
        int id,
        string ipAddress,
        string description)
    {
        Assert.AreEqual(id, address.ID);
        Assert.AreEqual(ipAddress, address.IPAddress);
        Assert.AreEqual(description, address.Description);
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

    private sealed class MutableGreyListingWhiteAddressAdministrationStore(
        IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> addresses)
        : IGreyListingWhiteAddressAdministrationStore
    {
        private IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> _addresses = addresses;

        public int ReadCount { get; private set; }

        public void Replace(IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> addresses)
        {
            _addresses = addresses;
        }

        public ValueTask<IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>> GetWhiteAddressesAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>>(
                _addresses.OrderBy(static address => address.StoredIpAddress, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }
}
