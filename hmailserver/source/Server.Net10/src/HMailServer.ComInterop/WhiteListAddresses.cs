using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("8492EE2E-7332-4253-B93E-D8B011B47D78")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceWhiteListAddresses
{
    [DispId(0)]
    IInterfaceWhiteListAddress this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceWhiteListAddress Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceWhiteListAddress get_ItemByDBID(int databaseId);

    [DispId(6)]
    void Refresh();

    [DispId(7)]
    void Clear();
}

[ComVisible(true)]
[Guid("D67457A7-3500-481F-900F-C9741C89D6AB")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceWhiteListAddress
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    string LowerIPAddress
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(3)]
    string UpperIPAddress
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(4)]
    string EmailAddress
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(5)]
    string Description
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(6)]
    void Save();

    [DispId(7)]
    void Delete();
}

[ComVisible(true)]
[Guid("FACFAF38-7BEE-48B4-A47E-D623ACCAE9AB")]
[ProgId("hMailServer.WhiteListAddresses.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceWhiteListAddresses))]
public sealed class WhiteListAddresses : IInterfaceWhiteListAddresses
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private WhiteListAddressAdministrationSnapshot[]? _addresses;
    private readonly Func<IReadOnlyList<WhiteListAddressAdministrationSnapshot>>? _reload;
    private readonly Func<WhiteListAddressAdministrationSnapshot, long>? _insert;
    private readonly Action<WhiteListAddressAdministrationSnapshot>? _update;
    private readonly Func<bool>? _isServerAdministrator;

    public WhiteListAddresses()
    {
    }

    private WhiteListAddresses(
        IReadOnlyList<WhiteListAddressAdministrationSnapshot> addresses,
        Func<IReadOnlyList<WhiteListAddressAdministrationSnapshot>>? reload,
        Func<WhiteListAddressAdministrationSnapshot, long>? insert,
        Action<WhiteListAddressAdministrationSnapshot>? update,
        Func<bool>? isServerAdministrator)
    {
        _addresses = addresses.ToArray();
        _reload = reload;
        _insert = insert;
        _update = update;
        _isServerAdministrator = isServerAdministrator;
    }

    public int Count => GetAddresses().Count;

    public IInterfaceWhiteListAddress this[int index]
    {
        get
        {
            var addresses = GetAddresses();
            if (index < 0 || index >= addresses.Count)
            {
                throw new COMException("Whitelist address index was outside the collection.", DispEBadIndex);
            }

            return CreateAddress(addresses[index]);
        }
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceWhiteListAddress Add()
    {
        _ = GetAddresses();
        EnsureServerAdministrator();
        if (_insert is null)
        {
            return Unavailable<IInterfaceWhiteListAddress>();
        }

        return WhiteListAddress.CreateAuthorized(
            new WhiteListAddressAdministrationSnapshot(
                Id: 0,
                LowerIpAddress: "0.0.0.0",
                UpperIpAddress: "0.0.0.0",
                EmailAddress: string.Empty,
                Description: string.Empty),
            save: SaveAddress,
            isServerAdministrator: _isServerAdministrator);
    }

    public IInterfaceWhiteListAddress get_ItemByDBID(int databaseId)
    {
        var match = GetAddresses().FirstOrDefault(
            address => unchecked((int)address.Id) == databaseId);

        return match is null
            ? throw new COMException("No whitelist address with the specified database identifier exists.", DispEBadIndex)
            : CreateAddress(match);
    }

    public void Refresh()
    {
        _ = GetAddresses();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var addresses = _reload();
            ArgumentNullException.ThrowIfNull(addresses);
            Volatile.Write(ref _addresses, addresses.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of whitelist addresses from the database.",
                EFail);
        }
    }

    public void Clear() => Unavailable();

    private WhiteListAddressAdministrationSnapshot SaveAddress(
        WhiteListAddressAdministrationSnapshot address)
    {
        var addresses = GetAddresses();
        if (address.Id != 0 && _update is null)
        {
            return Unavailable<WhiteListAddressAdministrationSnapshot>();
        }

        try
        {
            if (address.Id == 0)
            {
                if (_insert is null)
                {
                    return Unavailable<WhiteListAddressAdministrationSnapshot>();
                }

                var inserted = address with { Id = _insert(address) };
                if (inserted.Id <= 0)
                {
                    throw new InvalidOperationException("The whitelist insert did not return a valid database identifier.");
                }

                Volatile.Write(ref _addresses, addresses.Concat([inserted]).ToArray());
                return inserted;
            }

            _update!(address);
            Volatile.Write(
                ref _addresses,
                addresses.Select(existing => existing.Id == address.Id ? address : existing).ToArray());
            return address;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the whitelist address to the database.",
                EFail);
        }
    }

    internal static WhiteListAddresses CreateAuthorized(
        IReadOnlyList<WhiteListAddressAdministrationSnapshot> addresses,
        Func<IReadOnlyList<WhiteListAddressAdministrationSnapshot>>? reload = null,
        Func<WhiteListAddressAdministrationSnapshot, long>? insert = null,
        Action<WhiteListAddressAdministrationSnapshot>? update = null,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return new WhiteListAddresses(addresses, reload, insert, update, isServerAdministrator);
    }

    private WhiteListAddress CreateAddress(WhiteListAddressAdministrationSnapshot address) =>
        WhiteListAddress.CreateAuthorized(
            address,
            save: _insert is null && _update is null ? null : SaveAddress,
            isServerAdministrator: _isServerAdministrator);

    private IReadOnlyList<WhiteListAddressAdministrationSnapshot> GetAddresses()
    {
        return Volatile.Read(ref _addresses)
            ?? throw new COMException(
                "WhiteListAddresses access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "WhiteListAddresses access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void Unavailable()
    {
        _ = GetAddresses();
        throw new COMException(
            "This WhiteListAddresses member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private T Unavailable<T>()
    {
        Unavailable();
        return default!;
    }
}

[ComVisible(true)]
[Guid("0B18E4F3-4423-403E-B275-1D95CBD353CE")]
[ProgId("hMailServer.WhiteListAddress.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceWhiteListAddress))]
public sealed class WhiteListAddress : IInterfaceWhiteListAddress
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private WhiteListAddressAdministrationSnapshot? _address;
    private readonly Func<WhiteListAddressAdministrationSnapshot, WhiteListAddressAdministrationSnapshot>? _save;
    private readonly Func<bool>? _isServerAdministrator;

    public WhiteListAddress()
    {
    }

    private WhiteListAddress(
        WhiteListAddressAdministrationSnapshot address,
        Func<WhiteListAddressAdministrationSnapshot, WhiteListAddressAdministrationSnapshot>? save,
        Func<bool>? isServerAdministrator)
    {
        _address = address;
        _save = save;
        _isServerAdministrator = isServerAdministrator;
    }

    public int ID => unchecked((int)Snapshot.Id);

    public string LowerIPAddress
    {
        get => Snapshot.LowerIpAddress;
        set => Mutate(snapshot => snapshot with { LowerIpAddress = KeepLegacyAddress(snapshot.LowerIpAddress, value) });
    }

    public string UpperIPAddress
    {
        get => Snapshot.UpperIpAddress;
        set => Mutate(snapshot => snapshot with { UpperIpAddress = KeepLegacyAddress(snapshot.UpperIpAddress, value) });
    }

    public string EmailAddress { get => Snapshot.EmailAddress; set => Mutate(snapshot => snapshot with { EmailAddress = value ?? string.Empty }); }

    public string Description { get => Snapshot.Description; set => Mutate(snapshot => snapshot with { Description = value ?? string.Empty }); }

    public void Save()
    {
        EnsureServerAdministrator();
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _address = _save(Snapshot);
    }

    public void Delete() => Unavailable();

    internal static WhiteListAddress CreateAuthorized(
        WhiteListAddressAdministrationSnapshot address,
        Func<WhiteListAddressAdministrationSnapshot, WhiteListAddressAdministrationSnapshot>? save = null,
        Func<bool>? isServerAdministrator = null) =>
        new(address, save, isServerAdministrator);

    private WhiteListAddressAdministrationSnapshot Snapshot =>
        _address ?? throw new COMException(
            "WhiteListAddress access requires an authenticated server administrator.",
            EAccessDenied);

    private void Mutate(Func<WhiteListAddressAdministrationSnapshot, WhiteListAddressAdministrationSnapshot> mutation)
    {
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _address = mutation(Snapshot);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "WhiteListAddress access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private static string KeepLegacyAddress(string current, string? value)
    {
        return IPAddress.TryParse(value ?? string.Empty, out var parsed)
            ? parsed.ToString()
            : current;
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This WhiteListAddress member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class WhiteListAddressAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IWhiteListAddressAdministrationStore? _store;

    public static void Configure(IWhiteListAddressAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static WhiteListAddresses CreateAuthorizedAdapter(Func<bool>? isServerAdministrator = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer whitelist address administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<WhiteListAddressAdministrationSnapshot> LoadAddresses() => store
            .GetWhiteListAddressesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        long InsertAddress(WhiteListAddressAdministrationSnapshot address) => store
            .InsertWhiteListAddressAsync(address, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void UpdateAddress(WhiteListAddressAdministrationSnapshot address) => store
            .UpdateWhiteListAddressAsync(address, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return WhiteListAddresses.CreateAuthorized(
            LoadAddresses(),
            LoadAddresses,
            InsertAddress,
            UpdateAddress,
            isServerAdministrator);
    }
}
