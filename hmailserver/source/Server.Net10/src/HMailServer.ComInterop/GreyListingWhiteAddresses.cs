using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("D8D54486-4CC5-4240-A4BF-DD68D9C3E85B")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceGreyListingWhiteAddresses
{
    [DispId(0)]
    IInterfaceGreyListingWhiteAddress this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceGreyListingWhiteAddress Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceGreyListingWhiteAddress get_ItemByDBID(int databaseId);

    [DispId(6)]
    void Refresh();

    [DispId(7)]
    [SpecialName]
    IInterfaceGreyListingWhiteAddress get_ItemByName([MarshalAs(UnmanagedType.BStr)] string name);
}

[ComVisible(true)]
[Guid("A32DF62B-043F-4C0D-81E9-F4CC3CB62F33")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceGreyListingWhiteAddress
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    string IPAddress
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(3)]
    string Description
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(4)]
    void Save();

    [DispId(5)]
    void Delete();
}

[ComVisible(true)]
[Guid("F8BB11B8-5DD1-438E-AF29-6E088AA0BD06")]
[ProgId("hMailServer.GreyListingWhiteAddresses.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceGreyListingWhiteAddresses))]
public sealed class GreyListingWhiteAddresses : IInterfaceGreyListingWhiteAddresses
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private GreyListingWhiteAddressAdministrationSnapshot[]? _addresses;
    private readonly Func<IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>>? _reload;
    private readonly Func<GreyListingWhiteAddressAdministrationSnapshot, long>? _insert;
    private readonly Func<bool>? _isServerAdministrator;

    public GreyListingWhiteAddresses()
    {
    }

    private GreyListingWhiteAddresses(
        IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> addresses,
        Func<IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>>? reload,
        Func<GreyListingWhiteAddressAdministrationSnapshot, long>? insert,
        Func<bool>? isServerAdministrator)
    {
        _addresses = addresses.ToArray();
        _reload = reload;
        _insert = insert;
        _isServerAdministrator = isServerAdministrator;
    }

    public int Count => GetAddresses().Count;

    public IInterfaceGreyListingWhiteAddress this[int index]
    {
        get
        {
            var addresses = GetAddresses();
            if (index < 0 || index >= addresses.Count)
            {
                throw new COMException("Greylisting white address index was outside the collection.", DispEBadIndex);
            }

            return GreyListingWhiteAddress.CreateAuthorized(addresses[index]);
        }
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceGreyListingWhiteAddress Add()
    {
        _ = GetAddresses();
        EnsureServerAdministrator();
        if (_insert is null)
        {
            return Unavailable<IInterfaceGreyListingWhiteAddress>();
        }

        return GreyListingWhiteAddress.CreateAuthorized(
            new GreyListingWhiteAddressAdministrationSnapshot(0, string.Empty, string.Empty),
            insert: _insert,
            publish: Publish,
            isServerAdministrator: _isServerAdministrator);
    }

    public IInterfaceGreyListingWhiteAddress get_ItemByDBID(int databaseId)
    {
        var match = GetAddresses().FirstOrDefault(
            address => unchecked((int)address.Id) == databaseId);

        return match is null
            ? throw new COMException(
                "No greylisting white address with the specified database identifier exists.",
                DispEBadIndex)
            : GreyListingWhiteAddress.CreateAuthorized(match);
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
                "It was not possible to retrieve a list of greylisting white addresses from the database.",
                EFail);
        }
    }

    public IInterfaceGreyListingWhiteAddress get_ItemByName(string name)
    {
        var match = GetAddresses().FirstOrDefault(
            address => string.Equals(address.StoredIpAddress, name, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No greylisting white address with the specified name exists.", DispEBadIndex)
            : GreyListingWhiteAddress.CreateAuthorized(match);
    }

    internal static GreyListingWhiteAddresses CreateAuthorized(
        IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> addresses,
        Func<IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>>? reload = null,
        Func<GreyListingWhiteAddressAdministrationSnapshot, long>? insert = null,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return new GreyListingWhiteAddresses(addresses, reload, insert, isServerAdministrator);
    }

    private IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> GetAddresses()
    {
        return Volatile.Read(ref _addresses)
            ?? throw new COMException(
                "GreyListingWhiteAddresses access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void Publish(GreyListingWhiteAddressAdministrationSnapshot address)
    {
        var addresses = GetAddresses();
        Volatile.Write(ref _addresses, addresses.Append(address).ToArray());
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Greylisting white-address access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void Unavailable()
    {
        _ = GetAddresses();
        throw new COMException(
            "This GreyListingWhiteAddresses member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private T Unavailable<T>()
    {
        Unavailable();
        return default!;
    }
}

[ComVisible(true)]
[Guid("771EDD01-0E62-4071-AE72-88E439EC0880")]
[ProgId("hMailServer.GreyListingWhiteAddress.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceGreyListingWhiteAddress))]
public sealed class GreyListingWhiteAddress : IInterfaceGreyListingWhiteAddress
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private GreyListingWhiteAddressAdministrationSnapshot? _address;
    private readonly Func<GreyListingWhiteAddressAdministrationSnapshot, long>? _insert;
    private readonly Action<GreyListingWhiteAddressAdministrationSnapshot>? _publish;
    private readonly Func<bool>? _isServerAdministrator;

    public GreyListingWhiteAddress()
    {
    }

    private GreyListingWhiteAddress(GreyListingWhiteAddressAdministrationSnapshot address)
    {
        _address = address;
    }

    private GreyListingWhiteAddress(
        GreyListingWhiteAddressAdministrationSnapshot address,
        Func<GreyListingWhiteAddressAdministrationSnapshot, long>? insert,
        Action<GreyListingWhiteAddressAdministrationSnapshot>? publish,
        Func<bool>? isServerAdministrator)
    {
        _address = address;
        _insert = insert;
        _publish = publish;
        _isServerAdministrator = isServerAdministrator;
    }

    public int ID => unchecked((int)Snapshot.Id);

    public string IPAddress
    {
        get => ConvertLikeToWildcard(Snapshot.StoredIpAddress);
        set
        {
            EnsureServerAdministrator();
            if (_insert is null)
            {
                Unavailable();
                return;
            }

            _address = Snapshot with { StoredIpAddress = ConvertWildcardToLike(value ?? string.Empty) };
        }
    }

    public string Description
    {
        get => Snapshot.Description;
        set
        {
            EnsureServerAdministrator();
            if (_insert is null)
            {
                Unavailable();
                return;
            }

            _address = Snapshot with { Description = value ?? string.Empty };
        }
    }

    public void Save()
    {
        EnsureServerAdministrator();
        if (_insert is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var insertedId = _insert(Snapshot);
            if (insertedId <= 0)
            {
                throw new InvalidOperationException(
                    "The greylisting white-address insert did not return a valid generated identity.");
            }

            var saved = Snapshot with { Id = insertedId };
            _address = saved;
            _publish?.Invoke(saved);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the greylisting white address to the database.",
                EFail);
        }
    }

    public void Delete() => Unavailable();

    internal static GreyListingWhiteAddress CreateAuthorized(
        GreyListingWhiteAddressAdministrationSnapshot address,
        Func<GreyListingWhiteAddressAdministrationSnapshot, long>? insert = null,
        Action<GreyListingWhiteAddressAdministrationSnapshot>? publish = null,
        Func<bool>? isServerAdministrator = null) =>
        new(address, insert, publish, isServerAdministrator);

    internal static string ConvertLikeToWildcard(string value)
    {
        const string escapedPercentage = "\uE000";
        const string escapedUnderscore = "\uE001";

        return value
            .Replace("//", "/", StringComparison.Ordinal)
            .Replace("/%", escapedPercentage, StringComparison.Ordinal)
            .Replace("/_", escapedUnderscore, StringComparison.Ordinal)
            .Replace("_", "?", StringComparison.Ordinal)
            .Replace("%", "*", StringComparison.Ordinal)
            .Replace(escapedPercentage, "%", StringComparison.Ordinal)
            .Replace(escapedUnderscore, "_", StringComparison.Ordinal);
    }

    internal static string ConvertWildcardToLike(string value)
    {
        return value
            .Replace("/", "//", StringComparison.Ordinal)
            .Replace("%", "/%", StringComparison.Ordinal)
            .Replace("_", "/_", StringComparison.Ordinal)
            .Replace("?", "_", StringComparison.Ordinal)
            .Replace("*", "%", StringComparison.Ordinal);
    }

    private GreyListingWhiteAddressAdministrationSnapshot Snapshot =>
        _address ?? throw new COMException(
            "GreyListingWhiteAddress access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This GreyListingWhiteAddress member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Greylisting white-address access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }
}

[ComVisible(false)]
public static class GreyListingWhiteAddressAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IGreyListingWhiteAddressAdministrationStore? _store;

    public static void Configure(IGreyListingWhiteAddressAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static GreyListingWhiteAddresses CreateAuthorizedAdapter(
        Func<bool>? isServerAdministrator = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer greylisting white address administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> LoadAddresses() => store
            .GetWhiteAddressesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        long InsertAddress(GreyListingWhiteAddressAdministrationSnapshot address) => store
            .InsertWhiteAddressAsync(address, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return GreyListingWhiteAddresses.CreateAuthorized(
            LoadAddresses(),
            LoadAddresses,
            InsertAddress,
            isServerAdministrator);
    }
}
