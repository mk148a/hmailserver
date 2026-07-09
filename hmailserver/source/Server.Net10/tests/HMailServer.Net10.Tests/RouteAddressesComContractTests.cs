using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RouteAddressesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsMarshalingAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceRouteAddresses),
            "315BF27F-F832-4FBE-83FE-1C5A5011FAC7",
            new[]
            {
                "get_Item", "get_Count", "DeleteByDBID", "Add", "DeleteByAddress",
                "get_ItemByDBID"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceRouteAddresses).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        var deleteByAddress = typeof(IInterfaceRouteAddresses).GetMethod(
            nameof(IInterfaceRouteAddresses.DeleteByAddress));
        Assert.AreEqual(4, deleteByAddress?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.BStr,
            deleteByAddress?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceRouteAddress),
            "FD22CA52-BBF4-45BB-9165-986B3F4B5C77",
            new[]
            {
                "get_ID", "get_Address", "set_Address", "get_RouteID", "set_RouteID",
                "Save", "Delete"
            });
        var address = typeof(IInterfaceRouteAddress).GetProperty(nameof(IInterfaceRouteAddress.Address));
        Assert.AreEqual(2, address?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.BStr,
            address?.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.BStr,
            address?.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<RouteAddresses>(
            "2E66E5DC-DA9F-4490-A46F-E2D24C6CD151",
            "hMailServer.RouteAddresses.1",
            typeof(IInterfaceRouteAddresses));
        AssertComClass<RouteAddress>(
            "4CC5C4F5-7303-4C69-96D3-EC73ECF6F255",
            "hMailServer.RouteAddress.1",
            typeof(IInterfaceRouteAddress));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var addressesError = Assert.ThrowsExactly<COMException>(() => _ = new RouteAddresses().Count);
        var addressesDeleteError = Assert.ThrowsExactly<COMException>(() => new RouteAddresses().DeleteByDBID(100));
        var addressError = Assert.ThrowsExactly<COMException>(() => _ = new RouteAddress().Address);

        Assert.AreEqual(EAccessDenied, addressesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, addressesDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, addressError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        IInterfaceRouteAddresses addresses = RouteAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(100, 10, "alpha@example.test"),
                Snapshot(200, 10, "*@example.test")
            });

        Assert.AreEqual(2, addresses.Count);
        AssertAddress(addresses[0], 100, 10, "alpha@example.test");
        AssertAddress(addresses.get_ItemByDBID(200), 200, 10, "*@example.test");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = addresses[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByDBID(300));
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => addresses.DeleteByDBID(100));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => addresses.Add());
        var pendingDeleteByAddress = Assert.ThrowsExactly<COMException>(
            () => addresses.DeleteByAddress("alpha@example.test"));
        var pendingAddressMutation = Assert.ThrowsExactly<COMException>(
            () => addresses[0].Address = "changed@example.test");
        var pendingRouteMutation = Assert.ThrowsExactly<COMException>(() => addresses[0].RouteID = 20);
        var pendingSave = Assert.ThrowsExactly<COMException>(addresses[0].Save);
        var pendingItemDelete = Assert.ThrowsExactly<COMException>(addresses[0].Delete);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDeleteByAddress.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAddressMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRouteMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingItemDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByDBIDCallsConfiguredOperationAndRetainsSnapshotOnFailure()
    {
        var failDelete = true;
        var deletedIds = new List<int>();
        IInterfaceRouteAddresses addresses = RouteAddresses.CreateAuthorized(
            new[]
            {
                Snapshot(100, 10, "alpha@example.test"),
                Snapshot(200, 10, "*@example.test")
            },
            deleteById: databaseId =>
            {
                deletedIds.Add(databaseId);
                if (failDelete)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }
            });

        var deleteFailure = Assert.ThrowsExactly<COMException>(() => addresses.DeleteByDBID(100));

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(new[] { 100 }, deletedIds);
        Assert.AreEqual(2, addresses.Count);
        AssertAddress(addresses[0], 100, 10, "alpha@example.test");

        failDelete = false;
        addresses.DeleteByDBID(100);

        CollectionAssert.AreEqual(new[] { 100, 100 }, deletedIds);
        Assert.AreEqual(1, addresses.Count);
        AssertAddress(addresses[0], 200, 10, "*@example.test");
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByDBID(100)).ErrorCode);

        addresses.DeleteByDBID(999);

        CollectionAssert.AreEqual(new[] { 100, 100, 999 }, deletedIds);
        Assert.AreEqual(1, addresses.Count);
        AssertAddress(addresses[0], 200, 10, "*@example.test");
    }

    [TestMethod]
    public void AuthorizedRoute_UsesConfiguredRouteScopedRuntime()
    {
        var store = new FixedRouteAddressAdministrationStore(
            new[]
            {
                Snapshot(300, 20, "beta@example.test"),
                Snapshot(200, 10, "second@example.test"),
                Snapshot(100, 10, "first@example.test")
            });
        RouteAddressAdministrationRuntimeHost.Configure(
            store);
        var routes = Routes.CreateAuthorized(
            new[]
            {
                RouteSnapshot(10, "alpha.example"),
                RouteSnapshot(20, "beta.example")
            });

        var addresses = routes[0].Addresses;

        Assert.AreEqual(2, addresses.Count);
        Assert.AreEqual(100, addresses[0].ID);
        Assert.AreEqual("first@example.test", addresses[0].Address);
        var outsideRoute = Assert.ThrowsExactly<COMException>(() => _ = addresses.get_ItemByDBID(300));
        Assert.AreEqual(DispEBadIndex, outsideRoute.ErrorCode);

        addresses.DeleteByDBID(100);

        CollectionAssert.AreEqual(new[] { (RouteId: 10, DatabaseId: 100) }, store.DeletedAddresses);
        Assert.AreEqual(1, addresses.Count);
        AssertAddress(addresses[0], 200, 10, "second@example.test");

        addresses.DeleteByDBID(300);

        CollectionAssert.AreEqual(
            new[] { (RouteId: 10, DatabaseId: 100), (RouteId: 10, DatabaseId: 300) },
            store.DeletedAddresses);
        Assert.AreEqual(1, addresses.Count);
        AssertAddress(addresses[0], 200, 10, "second@example.test");
    }

    private static RouteAddressAdministrationSnapshot Snapshot(int id, int routeId, string address) =>
        new(id, routeId, address);

    private static RouteAdministrationSnapshot RouteSnapshot(int id, string domainName) =>
        new(id, domainName, string.Empty, string.Empty, 25, 4, 60, false, false, string.Empty, false, false, 0);

    private static void AssertAddress(
        IInterfaceRouteAddress address,
        int id,
        int routeId,
        string expectedAddress)
    {
        Assert.AreEqual(id, address.ID);
        Assert.AreEqual(routeId, address.RouteID);
        Assert.AreEqual(expectedAddress, address.Address);
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

    private sealed class FixedRouteAddressAdministrationStore(
        IReadOnlyList<RouteAddressAdministrationSnapshot> addresses)
        : IRouteAddressAdministrationStore
    {
        public List<(int RouteId, int DatabaseId)> DeletedAddresses { get; } = [];

        public ValueTask<IReadOnlyList<RouteAddressAdministrationSnapshot>> GetRouteAddressesAsync(
            int routeId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<RouteAddressAdministrationSnapshot>>(
                addresses.Where(address => address.RouteId == routeId)
                    .OrderBy(static address => address.Id)
                    .ToArray());

        public ValueTask DeleteRouteAddressByIdAsync(
            int routeId,
            int databaseId,
            CancellationToken cancellationToken)
        {
            DeletedAddresses.Add((routeId, databaseId));
            return ValueTask.CompletedTask;
        }
    }
}
