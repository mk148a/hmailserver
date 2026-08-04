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
    private readonly Func<RouteAddressAdministrationSnapshot, int>? _insert;
    private readonly Func<RouteAddressAdministrationSnapshot, bool>? _update;
    private readonly int? _owningRouteId;
    private readonly Func<bool>? _isServerAdministrator;

    public RouteAddresses()
    {
    }

    private RouteAddresses(
        IReadOnlyList<RouteAddressAdministrationSnapshot> addresses,
        Action<int>? deleteById,
        Func<RouteAddressAdministrationSnapshot, int>? insert,
        int? owningRouteId,
        Func<bool>? isServerAdministrator,
        Func<RouteAddressAdministrationSnapshot, bool>? update)
    {
        _addresses = addresses.ToArray();
        _deleteById = deleteById;
        _insert = insert;
        _update = update;
        _owningRouteId = owningRouteId;
        _isServerAdministrator = isServerAdministrator;
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

        if (!addresses.Any(address => address.Id == databaseId))
        {
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

    public IInterfaceRouteAddress Add()
    {
        _ = GetAddresses();
        if (_insert is null || _owningRouteId is null)
        {
            return Unavailable<IInterfaceRouteAddress>();
        }

        return RouteAddress.CreateAuthorized(
            new RouteAddressAdministrationSnapshot(0, _owningRouteId.Value, string.Empty),
            save: SaveRouteAddress,
            isServerAdministrator: _isServerAdministrator);
    }

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
        Action<int>? deleteById = null,
        Func<bool>? isServerAdministrator = null,
        Func<RouteAddressAdministrationSnapshot, int>? insert = null,
        int? owningRouteId = null,
        Func<RouteAddressAdministrationSnapshot, bool>? update = null)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return new RouteAddresses(
            addresses,
            deleteById,
            insert,
            owningRouteId,
            isServerAdministrator,
            update);
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
            save: _update is null ? null : SaveRouteAddress,
            delete: _deleteById is null ? null : DeleteByDBID,
            isServerAdministrator: _isServerAdministrator);
    }

    private RouteAddressAdministrationSnapshot SaveRouteAddress(RouteAddressAdministrationSnapshot address)
    {
        var addresses = GetAddresses();
        if (address.Id != 0 || _insert is null || _owningRouteId is null)
        {
            if (address.Id == 0 || _update is null || _owningRouteId is null ||
                !addresses.Any(existing => existing.Id == address.Id))
            {
                Unavailable();
                return address;
            }

            try
            {
                if (!_update(address))
                {
                    throw new InvalidOperationException(
                        "The route address update did not affect exactly one owning row.");
                }

                var updatedAddresses = addresses
                    .Select(existing => existing.Id == address.Id ? address : existing)
                    .ToArray();
                Volatile.Write(ref _addresses, updatedAddresses);
                return address;
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to save the route address to the database.",
                    EFail);
            }
        }

        try
        {
            var ownerScopedAddress = address with { RouteId = _owningRouteId.Value };
            var generatedId = _insert(ownerScopedAddress);
            if (generatedId <= 0)
            {
                throw new InvalidOperationException(
                    "The route address insert did not return a valid generated identity.");
            }

            var insertedAddress = ownerScopedAddress with { Id = generatedId };
            Volatile.Write(ref _addresses, addresses.Concat([insertedAddress]).ToArray());
            return insertedAddress;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the route address to the database.",
                EFail);
        }
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

    private RouteAddressAdministrationSnapshot? _address;
    private readonly Func<RouteAddressAdministrationSnapshot, RouteAddressAdministrationSnapshot>? _save;
    private readonly Action<int>? _delete;
    private readonly Func<bool>? _isServerAdministrator;

    public RouteAddress()
    {
    }

    private RouteAddress(
        RouteAddressAdministrationSnapshot address,
        Func<RouteAddressAdministrationSnapshot, RouteAddressAdministrationSnapshot>? save,
        Action<int>? delete,
        Func<bool>? isServerAdministrator)
    {
        _address = address;
        _save = save;
        _delete = delete;
        _isServerAdministrator = isServerAdministrator;
    }

    public int ID => Snapshot.Id;

    public string Address { get => Snapshot.Address; set => Mutate(snapshot => snapshot with { Address = value ?? string.Empty }); }

    public int RouteID { get => Snapshot.RouteId; set => Mutate(snapshot => snapshot with { RouteId = value }); }

    public void Save()
    {
        if (_save is null)
        {
            Unavailable();
            return;
        }

        EnsureServerAdministrator();
        _address = _save(Snapshot);
    }

    public void Delete()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "RouteAddress access requires an authenticated server administrator.",
                EAccessDenied);
        }

        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete(Snapshot.Id);
    }

    internal static RouteAddress CreateAuthorized(
        RouteAddressAdministrationSnapshot address,
        Func<RouteAddressAdministrationSnapshot, RouteAddressAdministrationSnapshot>? save = null,
        Action<int>? delete = null,
        Func<bool>? isServerAdministrator = null) =>
        new(address, save, delete, isServerAdministrator);

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

    private void Mutate(Func<RouteAddressAdministrationSnapshot, RouteAddressAdministrationSnapshot> mutation)
    {
        if (_save is null)
        {
            Unavailable();
            return;
        }

        EnsureServerAdministrator();
        _address = mutation(Snapshot);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "RouteAddress access requires an authenticated server administrator.",
                EAccessDenied);
        }
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

    internal static RouteAddresses CreateAuthorizedAdapter(
        int routeId,
        Func<bool>? isServerAdministrator = null)
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

        int InsertRouteAddress(RouteAddressAdministrationSnapshot address) => store
            .InsertRouteAddressAsync(routeId, address, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool UpdateRouteAddress(RouteAddressAdministrationSnapshot address) => store
            .UpdateRouteAddressAsync(routeId, address, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return RouteAddresses.CreateAuthorized(
            addresses,
            DeleteRouteAddressById,
            isServerAdministrator,
            InsertRouteAddress,
            routeId,
            UpdateRouteAddress);
    }
}
