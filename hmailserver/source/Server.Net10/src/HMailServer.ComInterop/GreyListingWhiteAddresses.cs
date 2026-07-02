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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot>? _addresses;

    public GreyListingWhiteAddresses()
    {
    }

    private GreyListingWhiteAddresses(IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> addresses)
    {
        _addresses = addresses.ToArray();
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

    public IInterfaceGreyListingWhiteAddress Add() => Unavailable<IInterfaceGreyListingWhiteAddress>();

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

    public void Refresh() => Unavailable();

    public IInterfaceGreyListingWhiteAddress get_ItemByName(string name)
    {
        var match = GetAddresses().FirstOrDefault(
            address => string.Equals(address.StoredIpAddress, name, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No greylisting white address with the specified name exists.", DispEBadIndex)
            : GreyListingWhiteAddress.CreateAuthorized(match);
    }

    internal static GreyListingWhiteAddresses CreateAuthorized(
        IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return new GreyListingWhiteAddresses(addresses);
    }

    private IReadOnlyList<GreyListingWhiteAddressAdministrationSnapshot> GetAddresses()
    {
        return _addresses
            ?? throw new COMException(
                "GreyListingWhiteAddresses access requires an authenticated server administrator.",
                EAccessDenied);
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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly GreyListingWhiteAddressAdministrationSnapshot? _address;

    public GreyListingWhiteAddress()
    {
    }

    private GreyListingWhiteAddress(GreyListingWhiteAddressAdministrationSnapshot address)
    {
        _address = address;
    }

    public int ID => unchecked((int)Snapshot.Id);

    public string IPAddress { get => ConvertLikeToWildcard(Snapshot.StoredIpAddress); set => Unavailable(); }

    public string Description { get => Snapshot.Description; set => Unavailable(); }

    public void Save() => Unavailable();

    public void Delete() => Unavailable();

    internal static GreyListingWhiteAddress CreateAuthorized(
        GreyListingWhiteAddressAdministrationSnapshot address) =>
        new(address);

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

    internal static GreyListingWhiteAddresses CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer greylisting white address administration runtime has not been initialized.",
                CoENotInitialized);

        var addresses = store
            .GetWhiteAddressesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return GreyListingWhiteAddresses.CreateAuthorized(addresses);
    }
}
