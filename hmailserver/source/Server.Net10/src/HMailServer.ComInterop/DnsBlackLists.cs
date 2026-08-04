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
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private DnsBlackListAdministrationSnapshot[]? _blackLists;
    private readonly Func<IReadOnlyList<DnsBlackListAdministrationSnapshot>>? _reload;
    private readonly Func<DnsBlackListAdministrationSnapshot, int>? _insert;
    private readonly Action<DnsBlackListAdministrationSnapshot>? _publish;
    private readonly Func<bool>? _isServerAdministrator;

    public DNSBlackLists()
    {
    }

    private DNSBlackLists(
        IReadOnlyList<DnsBlackListAdministrationSnapshot> blackLists,
        Func<IReadOnlyList<DnsBlackListAdministrationSnapshot>>? reload,
        Func<DnsBlackListAdministrationSnapshot, int>? insert,
        Func<bool>? isServerAdministrator)
    {
        _blackLists = blackLists.ToArray();
        _reload = reload;
        _insert = insert;
        _isServerAdministrator = isServerAdministrator;
        _publish = Publish;
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

            return DNSBlackList.CreateAuthorized(
                blackLists[index],
                isServerAdministrator: _isServerAdministrator);
        }
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceDNSBlackList Add()
    {
        _ = GetBlackLists();
        EnsureServerAdministrator();
        if (_insert is null)
        {
            return Unavailable<IInterfaceDNSBlackList>();
        }

        return DNSBlackList.CreateAuthorized(
            new DnsBlackListAdministrationSnapshot(0, false, string.Empty, string.Empty, string.Empty, 0),
            insert: _insert,
            publish: _publish,
            isServerAdministrator: _isServerAdministrator);
    }

    public IInterfaceDNSBlackList get_ItemByDBID(int databaseId)
    {
        var match = GetBlackLists().FirstOrDefault(blackList => blackList.Id == databaseId);

        return match is null
            ? throw new COMException("No DNS blacklist with the specified database identifier exists.", DispEBadIndex)
            : DNSBlackList.CreateAuthorized(
                match,
                isServerAdministrator: _isServerAdministrator);
    }

    public void Refresh()
    {
        _ = GetBlackLists();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var blackLists = _reload();
            ArgumentNullException.ThrowIfNull(blackLists);
            Volatile.Write(ref _blackLists, blackLists.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of DNS blacklists from the database.",
                EFail);
        }
    }

    public IInterfaceDNSBlackList get_ItemByDNSHost(string dnsHost)
    {
        var match = GetBlackLists().FirstOrDefault(
            blackList => string.Equals(blackList.DnsHost, dnsHost, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? null!
            : DNSBlackList.CreateAuthorized(
                match,
                isServerAdministrator: _isServerAdministrator);
    }

    internal static DNSBlackLists CreateAuthorized(
        IReadOnlyList<DnsBlackListAdministrationSnapshot> blackLists,
        Func<IReadOnlyList<DnsBlackListAdministrationSnapshot>>? reload = null,
        Func<DnsBlackListAdministrationSnapshot, int>? insert = null,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(blackLists);
        return new DNSBlackLists(blackLists, reload, insert, isServerAdministrator);
    }

    private IReadOnlyList<DnsBlackListAdministrationSnapshot> GetBlackLists()
    {
        return Volatile.Read(ref _blackLists)
            ?? throw new COMException(
                "DNSBlackLists access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void Publish(DnsBlackListAdministrationSnapshot blackList)
    {
        var blackLists = GetBlackLists();
        Volatile.Write(ref _blackLists, blackLists.Append(blackList).ToArray());
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "DNSBlackLists access requires an authenticated server administrator.",
                EAccessDenied);
        }
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
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private DnsBlackListAdministrationSnapshot? _blackList;
    private readonly Func<DnsBlackListAdministrationSnapshot, int>? _insert;
    private readonly Action<DnsBlackListAdministrationSnapshot>? _publish;
    private readonly Func<bool>? _isServerAdministrator;

    public DNSBlackList()
    {
    }

    private DNSBlackList(DnsBlackListAdministrationSnapshot blackList)
    {
        _blackList = blackList;
    }

    private DNSBlackList(
        DnsBlackListAdministrationSnapshot blackList,
        Func<DnsBlackListAdministrationSnapshot, int>? insert,
        Action<DnsBlackListAdministrationSnapshot>? publish,
        Func<bool>? isServerAdministrator)
    {
        _blackList = blackList;
        _insert = insert;
        _publish = publish;
        _isServerAdministrator = isServerAdministrator;
    }

    public bool Active
    {
        get => Snapshot.Active;
        set => Mutate(snapshot => snapshot with { Active = value });
    }

    public int ID => Snapshot.Id;

    public string DNSHost
    {
        get => Snapshot.DnsHost;
        set => Mutate(snapshot => snapshot with { DnsHost = value ?? string.Empty });
    }

    public string RejectMessage
    {
        get => Snapshot.RejectMessage;
        set => Mutate(snapshot => snapshot with { RejectMessage = value ?? string.Empty });
    }

    public string ExpectedResult
    {
        get => Snapshot.ExpectedResult;
        set => Mutate(snapshot => snapshot with { ExpectedResult = value ?? string.Empty });
    }

    public void Save()
    {
        EnsureServerAdministrator();
        if (Snapshot.Id != 0 || _insert is null)
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
                    "The DNS blacklist insert did not return a valid generated identity.");
            }

            var saved = Snapshot with { Id = insertedId };
            _blackList = saved;
            _publish?.Invoke(saved);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the DNS blacklist to the database.",
                EFail);
        }
    }

    public int Score
    {
        get => Snapshot.Score;
        set => Mutate(snapshot => snapshot with { Score = value });
    }

    public void Delete() => Unavailable();

    internal static DNSBlackList CreateAuthorized(
        DnsBlackListAdministrationSnapshot blackList,
        Func<DnsBlackListAdministrationSnapshot, int>? insert = null,
        Action<DnsBlackListAdministrationSnapshot>? publish = null,
        Func<bool>? isServerAdministrator = null) =>
        new(blackList, insert, publish, isServerAdministrator);

    private DnsBlackListAdministrationSnapshot Snapshot =>
        _blackList ?? throw new COMException(
            "DNSBlackList access requires an authenticated server administrator.",
            EAccessDenied);

    private void Mutate(Func<DnsBlackListAdministrationSnapshot, DnsBlackListAdministrationSnapshot> mutation)
    {
        EnsureServerAdministrator();
        if (_insert is null)
        {
            Unavailable();
            return;
        }

        _blackList = mutation(Snapshot);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "DNSBlackList access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

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

    internal static DNSBlackLists CreateAuthorizedAdapter(
        Func<bool>? isServerAdministrator = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer DNS blacklist administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<DnsBlackListAdministrationSnapshot> LoadBlackLists() => store
            .GetDnsBlackListsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertBlackList(DnsBlackListAdministrationSnapshot blackList) => store
            .InsertDnsBlackListAsync(blackList, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return DNSBlackLists.CreateAuthorized(
            LoadBlackLists(),
            LoadBlackLists,
            InsertBlackList,
            isServerAdministrator);
    }
}
