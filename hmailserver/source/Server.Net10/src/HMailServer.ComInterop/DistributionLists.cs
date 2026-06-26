using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("8F0E22B8-0824-42DF-9260-F8B9ABFA8C61")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDistributionLists
{
    [DispId(0)]
    IInterfaceDistributionList this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    [SpecialName]
    IInterfaceDistributionList get_ItemByDBID(int databaseId);

    [DispId(3)]
    IInterfaceDistributionList Add();

    [DispId(4)]
    void DeleteByDBID(int databaseId);

    [DispId(5)]
    [SpecialName]
    IInterfaceDistributionList get_ItemByAddress([MarshalAs(UnmanagedType.BStr)] string address);

    [DispId(6)]
    void Refresh();
}

[ComVisible(true)]
[Guid("8251393D-27D8-4DF2-8A05-949C11D42C09")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDistributionList
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    void Delete();

    [DispId(4)]
    void Save();

    [DispId(5)]
    bool Active
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(6)]
    IInterfaceDistributionListRecipients Recipients { get; }

    [DispId(7)]
    string Address
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(8)]
    bool RequireSMTPAuth
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(9)]
    string RequireSenderAddress
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(10)]
    ComDistributionListMode Mode { get; set; }
}

[ComVisible(true)]
[Guid("C3DD0A4A-0551-442F-859A-76AAB92A6CF1")]
[ProgId("hMailServer.DistributionLists.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDistributionLists))]
public sealed class DistributionLists : IInterfaceDistributionLists
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<DistributionListAdministrationSnapshot>? _lists;

    public DistributionLists()
    {
    }

    private DistributionLists(IReadOnlyList<DistributionListAdministrationSnapshot> lists)
    {
        _lists = lists.ToArray();
    }

    public int Count => GetLists().Count;

    internal static DistributionLists CreateAuthorized(IReadOnlyList<DistributionListAdministrationSnapshot> lists)
    {
        ArgumentNullException.ThrowIfNull(lists);
        return new DistributionLists(lists);
    }

    public IInterfaceDistributionList this[int index]
    {
        get
        {
            var lists = GetLists();
            if (index < 0 || index >= lists.Count)
            {
                throw new COMException("Distribution list index was outside the collection.", DispEBadIndex);
            }

            return DistributionList.CreateAuthorized(lists[index]);
        }
    }

    public IInterfaceDistributionList get_ItemByDBID(int databaseId)
    {
        var match = GetLists().FirstOrDefault(list => list.Id == databaseId);

        return match is null
            ? throw new COMException("No distribution list with the specified database identifier exists.", DispEBadIndex)
            : DistributionList.CreateAuthorized(match);
    }

    public IInterfaceDistributionList Add() => Unavailable<IInterfaceDistributionList>();

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceDistributionList get_ItemByAddress(string address)
    {
        var match = GetLists()
            .FirstOrDefault(list => list.Address.Equals(address, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No distribution list with the specified address exists.", DispEBadIndex)
            : DistributionList.CreateAuthorized(match);
    }

    public void Refresh() => Unavailable();

    private IReadOnlyList<DistributionListAdministrationSnapshot> GetLists()
    {
        return _lists
            ?? throw new COMException(
                "DistributionLists access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetLists();
        throw new COMException(
            "This DistributionLists member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetLists();
        throw new COMException(
            "This DistributionLists member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("990D27ED-86CE-4DCB-B1C1-1E130C07F918")]
[ProgId("hMailServer.DistributionList.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDistributionList))]
public sealed class DistributionList : IInterfaceDistributionList
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly DistributionListAdministrationSnapshot? _list;

    public DistributionList()
    {
    }

    private DistributionList(DistributionListAdministrationSnapshot list)
    {
        _list = list;
    }

    public int ID => Snapshot.Id;

    public bool Active
    {
        get => Snapshot.Active;
        set => Unavailable();
    }

    public IInterfaceDistributionListRecipients Recipients =>
        DistributionListRecipientAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id);

    public string Address
    {
        get => Snapshot.Address;
        set => Unavailable();
    }

    public bool RequireSMTPAuth
    {
        get => Snapshot.RequireSmtpAuth;
        set => Unavailable();
    }

    public string RequireSenderAddress
    {
        get => Snapshot.RequireSenderAddress;
        set => Unavailable();
    }

    public ComDistributionListMode Mode
    {
        get => (ComDistributionListMode)Snapshot.Mode;
        set => Unavailable();
    }

    internal static DistributionList CreateAuthorized(DistributionListAdministrationSnapshot list) => new(list);

    public void Delete() => Unavailable();

    public void Save() => Unavailable();

    private DistributionListAdministrationSnapshot Snapshot =>
        _list ?? throw new COMException(
            "DistributionList access requires an authenticated server administrator.",
            EAccessDenied);

    private T Unavailable<T>()
    {
        _ = Snapshot;
        throw new COMException(
            "This DistributionList member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This DistributionList member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class DistributionListAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IDistributionListAdministrationStore? _store;

    public static void Configure(IDistributionListAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static DistributionLists CreateAuthorizedAdapter(int domainId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer distribution-list administration runtime has not been initialized.",
                CoENotInitialized);

        var lists = store
            .GetDistributionListsAsync(domainId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return DistributionLists.CreateAuthorized(lists);
    }
}
