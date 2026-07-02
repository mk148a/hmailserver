using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("6B87D71F-93B7-4163-AA89-DA999A5A7239")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDNSBlackLists
{
    [DispId(0)]
    IInterfaceDNSBlackList this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceDNSBlackList Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceDNSBlackList get_ItemByDBID(int databaseId);

    [DispId(6)]
    void Refresh();

    [DispId(7)]
    [SpecialName]
    IInterfaceDNSBlackList get_ItemByDNSHost([MarshalAs(UnmanagedType.BStr)] string dnsHost);
}

[ComVisible(true)]
[Guid("6E011153-63D9-4B86-BA97-E55D152B221D")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDNSBlackList
{
    [DispId(1)]
    bool Active
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(2)]
    int ID { get; }

    [DispId(3)]
    string DNSHost
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(4)]
    string RejectMessage
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(5)]
    string ExpectedResult
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(6)]
    void Save();

    [DispId(7)]
    int Score { get; set; }

    [DispId(8)]
    void Delete();
}

[ComVisible(true)]
[Guid("39ECFFB4-B9EE-46C2-A84B-32D679FB3C82")]
[ProgId("hMailServer.DNSBlackLists.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDNSBlackLists))]
public sealed class DNSBlackLists : IInterfaceDNSBlackLists
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<DnsBlackListAdministrationSnapshot>? _blackLists;

    public DNSBlackLists()
    {
    }

    private DNSBlackLists(IReadOnlyList<DnsBlackListAdministrationSnapshot> blackLists)
    {
        _blackLists = blackLists.ToArray();
    }

    public int Count => GetBlackLists().Count;

    public IInterfaceDNSBlackList this[int index]
    {
        get
        {
            var blackLists = GetBlackLists();
            if (index < 0 || index >= blackLists.Count)
            {
                throw new COMException("DNS blacklist index was outside the collection.", DispEBadIndex);
            }

            return DNSBlackList.CreateAuthorized(blackLists[index]);
        }
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceDNSBlackList Add() => Unavailable<IInterfaceDNSBlackList>();

    public IInterfaceDNSBlackList get_ItemByDBID(int databaseId)
    {
        var match = GetBlackLists().FirstOrDefault(blackList => blackList.Id == databaseId);

        return match is null
            ? throw new COMException("No DNS blacklist with the specified database identifier exists.", DispEBadIndex)
            : DNSBlackList.CreateAuthorized(match);
    }

    public void Refresh() => Unavailable();

    public IInterfaceDNSBlackList get_ItemByDNSHost(string dnsHost)
    {
        var match = GetBlackLists().FirstOrDefault(
            blackList => string.Equals(blackList.DnsHost, dnsHost, StringComparison.OrdinalIgnoreCase));

        return match is null ? null! : DNSBlackList.CreateAuthorized(match);
    }

    internal static DNSBlackLists CreateAuthorized(IReadOnlyList<DnsBlackListAdministrationSnapshot> blackLists)
    {
        ArgumentNullException.ThrowIfNull(blackLists);
        return new DNSBlackLists(blackLists);
    }

    private IReadOnlyList<DnsBlackListAdministrationSnapshot> GetBlackLists()
    {
        return _blackLists
            ?? throw new COMException(
                "DNSBlackLists access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void Unavailable()
    {
        _ = GetBlackLists();
        throw new COMException(
            "This DNSBlackLists member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private T Unavailable<T>()
    {
        Unavailable();
        return default!;
    }
}

[ComVisible(true)]
[Guid("E5907F7D-F13E-4D8A-A7DE-A29717C75A8F")]
[ProgId("hMailServer.DNSBlackList.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDNSBlackList))]
public sealed class DNSBlackList : IInterfaceDNSBlackList
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly DnsBlackListAdministrationSnapshot? _blackList;

    public DNSBlackList()
    {
    }

    private DNSBlackList(DnsBlackListAdministrationSnapshot blackList)
    {
        _blackList = blackList;
    }

    public bool Active { get => Snapshot.Active; set => Unavailable(); }

    public int ID => Snapshot.Id;

    public string DNSHost { get => Snapshot.DnsHost; set => Unavailable(); }

    public string RejectMessage { get => Snapshot.RejectMessage; set => Unavailable(); }

    public string ExpectedResult { get => Snapshot.ExpectedResult; set => Unavailable(); }

    public void Save() => Unavailable();

    public int Score { get => Snapshot.Score; set => Unavailable(); }

    public void Delete() => Unavailable();

    internal static DNSBlackList CreateAuthorized(DnsBlackListAdministrationSnapshot blackList) => new(blackList);

    private DnsBlackListAdministrationSnapshot Snapshot =>
        _blackList ?? throw new COMException(
            "DNSBlackList access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This DNSBlackList member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class DnsBlackListAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IDnsBlackListAdministrationStore? _store;

    public static void Configure(IDnsBlackListAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static DNSBlackLists CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer DNS blacklist administration runtime has not been initialized.",
                CoENotInitialized);

        var blackLists = store
            .GetDnsBlackListsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return DNSBlackLists.CreateAuthorized(blackLists);
    }
}
