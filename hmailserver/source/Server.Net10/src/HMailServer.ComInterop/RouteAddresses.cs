using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("315BF27F-F832-4FBE-83FE-1C5A5011FAC7")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRouteAddresses
{
    [DispId(0)]
    IInterfaceRouteAddress this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceRouteAddress Add();

    [DispId(4)]
    void DeleteByAddress([MarshalAs(UnmanagedType.BStr)] string address);

    [DispId(5)]
    [SpecialName]
    IInterfaceRouteAddress get_ItemByDBID(int databaseId);
}

[ComVisible(true)]
[Guid("FD22CA52-BBF4-45BB-9165-986B3F4B5C77")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRouteAddress
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    string Address
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(3)]
    int RouteID { get; set; }

    [DispId(4)]
    void Save();

    [DispId(5)]
    void Delete();
}

[ComVisible(true)]
[Guid("2E66E5DC-DA9F-4490-A46F-E2D24C6CD151")]
[ProgId("hMailServer.RouteAddresses.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRouteAddresses))]
public sealed class RouteAddresses : IInterfaceRouteAddresses
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private RouteAddressAdministrationSnapshot[]? _addresses;
    private readonly Action<int>? _deleteById;

    public RouteAddresses()
    {
    }

    private RouteAddresses(
        IReadOnlyList<RouteAddressAdministrationSnapshot> addresses,
        Action<int>? deleteById)
    {
        _addresses = addresses.ToArray();
        _deleteById = deleteById;
    }

    public int Count => GetAddresses().Count;

    public IInterfaceRouteAddress this[int index]
    {
        get
        {
            var addresses = GetAddresses();
            if (index < 0 || index >= addresses.Count)
            {
                throw new COMException("Route address index was outside the collection.", DispEBadIndex);
            }

            return CreateAddress(addresses[index]);
        }
    }

    public IInterfaceRouteAddress get_ItemByDBID(int databaseId)
    {
        var match = GetAddresses().FirstOrDefault(address => address.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No route address with the specified database identifier exists.",
                DispEBadIndex)
            : CreateAddress(match);
    }

    public void DeleteByDBID(int databaseId)
    {
        var addresses = GetAddresses();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _deleteById(databaseId);
            Volatile.Write(
                ref _addresses,
                addresses.Where(address => address.Id != databaseId).ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the route address from the database.",
                EFail);
        }
    }

    public IInterfaceRouteAddress Add() => Unavailable<IInterfaceRouteAddress>();

    public void DeleteByAddress(string address)
    {
        var match = GetAddresses().FirstOrDefault(
            routeAddress => string.Equals(
                routeAddress.Address,
                address ?? string.Empty,
                StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return;
        }

        DeleteByDBID(match.Id);
    }

    internal static RouteAddresses CreateAuthorized(
        IReadOnlyList<RouteAddressAdministrationSnapshot> addresses,
        Action<int>? deleteById = null)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return new RouteAddresses(addresses, deleteById);
    }

    private IReadOnlyList<RouteAddressAdministrationSnapshot> GetAddresses()
    {
        return Volatile.Read(ref _addresses)
            ?? throw new COMException(
                "RouteAddresses access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private RouteAddress CreateAddress(RouteAddressAdministrationSnapshot address)
    {
        return RouteAddress.CreateAuthorized(
            address,
            delete: _deleteById is null ? null : DeleteByDBID);
    }

    private T Unavailable<T>()
    {
        _ = GetAddresses();
        throw new COMException(
            "This RouteAddresses member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetAddresses();
        throw new COMException(
            "This RouteAddresses member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("4CC5C4F5-7303-4C69-96D3-EC73ECF6F255")]
[ProgId("hMailServer.RouteAddress.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRouteAddress))]
public sealed class RouteAddress : IInterfaceRouteAddress
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly RouteAddressAdministrationSnapshot? _address;
    private readonly Action<int>? _delete;

    public RouteAddress()
    {
    }

    private RouteAddress(
        RouteAddressAdministrationSnapshot address,
        Action<int>? delete)
    {
        _address = address;
        _delete = delete;
    }

    public int ID => Snapshot.Id;

    public string Address { get => Snapshot.Address; set => Unavailable(); }

    public int RouteID { get => Snapshot.RouteId; set => Unavailable(); }

    public void Save() => Unavailable();

    public void Delete()
    {
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete(Snapshot.Id);
    }

    internal static RouteAddress CreateAuthorized(
        RouteAddressAdministrationSnapshot address,
        Action<int>? delete = null) =>
        new(address, delete);

    private RouteAddressAdministrationSnapshot Snapshot =>
        _address ?? throw new COMException(
            "RouteAddress access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This RouteAddress member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class RouteAddressAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IRouteAddressAdministrationStore? _store;

    public static void Configure(IRouteAddressAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static RouteAddresses CreateAuthorizedAdapter(int routeId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer route address administration runtime has not been initialized.",
                CoENotInitialized);

        var addresses = store
            .GetRouteAddressesAsync(routeId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void DeleteRouteAddressById(int databaseId) => store
            .DeleteRouteAddressByIdAsync(routeId, databaseId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return RouteAddresses.CreateAuthorized(addresses, DeleteRouteAddressById);
    }
}
