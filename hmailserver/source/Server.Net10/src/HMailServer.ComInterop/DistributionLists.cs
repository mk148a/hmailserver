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
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private DistributionListAdministrationSnapshot[]? _lists;
    private readonly Func<IReadOnlyList<DistributionListAdministrationSnapshot>>? _reload;
    private readonly Func<DistributionListAdministrationSnapshot, int>? _insert;
    private readonly Func<DistributionListAdministrationSnapshot, bool>? _update;
    private readonly Func<int, bool>? _delete;
    private readonly Func<bool>? _isAuthenticated;
    private readonly int _domainId;

    public DistributionLists()
    {
    }

    private DistributionLists(
        IReadOnlyList<DistributionListAdministrationSnapshot> lists,
        Func<IReadOnlyList<DistributionListAdministrationSnapshot>>? reload,
        Func<DistributionListAdministrationSnapshot, int>? insert,
        Func<DistributionListAdministrationSnapshot, bool>? update,
        Func<int, bool>? delete,
        Func<bool>? isAuthenticated,
        int domainId)
    {
        _lists = lists.ToArray();
        _reload = reload;
        _insert = insert;
        _update = update;
        _delete = delete;
        _isAuthenticated = isAuthenticated;
        _domainId = domainId;
    }

    public int Count => GetLists().Count;

    internal static DistributionLists CreateAuthorized(
        IReadOnlyList<DistributionListAdministrationSnapshot> lists,
        Func<IReadOnlyList<DistributionListAdministrationSnapshot>>? reload = null,
        Func<DistributionListAdministrationSnapshot, int>? insert = null,
        Func<DistributionListAdministrationSnapshot, bool>? update = null,
        Func<int, bool>? delete = null,
        Func<bool>? isAuthenticated = null,
        int domainId = 0)
    {
        ArgumentNullException.ThrowIfNull(lists);
        return new DistributionLists(lists, reload, insert, update, delete, isAuthenticated, domainId);
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

            return DistributionList.CreateAuthorized(
                lists[index],
                update: _update,
                replace: Replace,
                delete: _delete is null ? null : DeleteExistingList,
                isAuthenticated: _isAuthenticated);
        }
    }

    public IInterfaceDistributionList get_ItemByDBID(int databaseId)
    {
        var match = GetLists().FirstOrDefault(list => list.Id == databaseId);

        return match is null
            ? throw new COMException("No distribution list with the specified database identifier exists.", DispEBadIndex)
            : DistributionList.CreateAuthorized(
                match,
                update: _update,
                replace: Replace,
                delete: _delete is null ? null : DeleteExistingList,
                isAuthenticated: _isAuthenticated);
    }

    public IInterfaceDistributionList Add()
    {
        _ = GetLists();
        EnsureAuthenticated();
        if (_insert is null || _isAuthenticated is null)
        {
            return Unavailable<IInterfaceDistributionList>();
        }

        return DistributionList.CreateAuthorized(
            new DistributionListAdministrationSnapshot(
                Id: 0,
                DomainId: _domainId,
                Address: string.Empty,
                Active: false,
                RequireSmtpAuth: false,
                RequireSenderAddress: string.Empty,
                Mode: (int)ComDistributionListMode.Public),
            insert: _insert,
            append: Append,
            delete: _delete is null ? null : DeleteExistingList,
            isAuthenticated: _isAuthenticated);
    }

    public void DeleteByDBID(int databaseId)
    {
        _ = GetLists();
        EnsureAuthenticated();
        if (_delete is null)
        {
            Unavailable();
        }

        if (!GetLists().Any(list => list.Id == databaseId))
        {
            return;
        }

        DeleteExistingList(databaseId);
    }

    public IInterfaceDistributionList get_ItemByAddress(string address)
    {
        var match = GetLists()
            .FirstOrDefault(list => list.Address.Equals(address, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No distribution list with the specified address exists.", DispEBadIndex)
            : DistributionList.CreateAuthorized(
                match,
                update: _update,
                replace: Replace,
                isAuthenticated: _isAuthenticated);
    }

    public void Refresh()
    {
        _ = GetLists();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var lists = _reload();
            ArgumentNullException.ThrowIfNull(lists);
            Volatile.Write(ref _lists, lists.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of distribution lists from the database.",
                EFail);
        }
    }

    private IReadOnlyList<DistributionListAdministrationSnapshot> GetLists()
    {
        return Volatile.Read(ref _lists)
            ?? throw new COMException(
                "DistributionLists access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void Append(DistributionListAdministrationSnapshot list)
    {
        var lists = GetLists();
        Volatile.Write(ref _lists, lists.Append(list).ToArray());
    }

    private void Replace(DistributionListAdministrationSnapshot list)
    {
        var lists = GetLists();
        Volatile.Write(
            ref _lists,
            lists.Select(existing => existing.Id == list.Id ? list : existing).ToArray());
    }

    private void DeleteExistingList(int databaseId)
    {
        EnsureAuthenticated();
        var lists = GetLists();
        if (_delete is null)
        {
            Unavailable();
        }

        if (databaseId == 0)
        {
            throw new InvalidOperationException("A distribution list with database identifier zero cannot be deleted.");
        }

        if (!lists.Any(list => list.Id == databaseId))
        {
            return;
        }

        try
        {
            if (!_delete!(databaseId))
            {
                throw new InvalidOperationException(
                    $"Deleting distribution list {databaseId} for domain {_domainId} did not affect one row.");
            }

            Volatile.Write(
                ref _lists,
                lists.Where(list => list.Id != databaseId).ToArray());
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the distribution list from the database.",
                EFail);
        }
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "DistributionLists access requires an authenticated server administrator.",
                EAccessDenied);
        }
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

    private DistributionListAdministrationSnapshot? _list;
    private readonly Func<DistributionListAdministrationSnapshot, int>? _insert;
    private readonly Func<DistributionListAdministrationSnapshot, bool>? _update;
    private readonly Action<DistributionListAdministrationSnapshot>? _append;
    private readonly Action<DistributionListAdministrationSnapshot>? _replace;
    private readonly Action<int>? _delete;
    private readonly Func<bool>? _isAuthenticated;

    public DistributionList()
    {
    }

    private DistributionList(
        DistributionListAdministrationSnapshot list,
        Func<DistributionListAdministrationSnapshot, int>? insert,
        Func<DistributionListAdministrationSnapshot, bool>? update,
        Action<DistributionListAdministrationSnapshot>? append,
        Action<DistributionListAdministrationSnapshot>? replace,
        Action<int>? delete,
        Func<bool>? isAuthenticated)
    {
        _list = list;
        _insert = insert;
        _update = update;
        _append = append;
        _replace = replace;
        _delete = delete;
        _isAuthenticated = isAuthenticated;
    }

    public int ID => Snapshot.Id;

    public bool Active
    {
        get => Snapshot.Active;
        set => Mutate(snapshot => snapshot with { Active = value });
    }

    public IInterfaceDistributionListRecipients Recipients
    {
        get
        {
            EnsureAuthenticated();
            return DistributionListRecipientAdministrationRuntimeHost.CreateAuthorizedAdapter(
                Snapshot.Id,
                _isAuthenticated);
        }
    }

    public string Address
    {
        get => Snapshot.Address;
        set => Mutate(snapshot => snapshot with { Address = value ?? string.Empty });
    }

    public bool RequireSMTPAuth
    {
        get => Snapshot.RequireSmtpAuth;
        set => Mutate(snapshot => snapshot with { RequireSmtpAuth = value });
    }

    public string RequireSenderAddress
    {
        get => Snapshot.RequireSenderAddress;
        set => Mutate(snapshot => snapshot with { RequireSenderAddress = value ?? string.Empty });
    }

    public ComDistributionListMode Mode
    {
        get => (ComDistributionListMode)Snapshot.Mode;
        set => Mutate(snapshot => snapshot with { Mode = (int)value });
    }

    internal static DistributionList CreateAuthorized(
        DistributionListAdministrationSnapshot list,
        Func<DistributionListAdministrationSnapshot, int>? insert = null,
        Func<DistributionListAdministrationSnapshot, bool>? update = null,
        Action<DistributionListAdministrationSnapshot>? append = null,
        Action<DistributionListAdministrationSnapshot>? replace = null,
        Action<int>? delete = null,
        Func<bool>? isAuthenticated = null) =>
        new(list, insert, update, append, replace, delete, isAuthenticated);

    public void Delete()
    {
        EnsureAuthenticated();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _delete(Snapshot.Id);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the distribution list from the database.",
                unchecked((int)0x80004005));
        }
    }

    public void Save()
    {
        EnsureAuthenticated();
        var snapshot = Snapshot;
        if ((snapshot.Id == 0 && (_insert is null || _isAuthenticated is null)) ||
            (snapshot.Id != 0 && (_update is null || _isAuthenticated is null)))
        {
            Unavailable();
            return;
        }

        try
        {
            if (snapshot.Id == 0)
            {
                var insertedId = _insert!(snapshot);
                if (insertedId <= 0)
                {
                    throw new InvalidOperationException(
                        "The distribution list insert did not return a valid generated identity.");
                }

                var saved = snapshot with { Id = insertedId };
                _list = saved;
                _append?.Invoke(saved);
                return;
            }

            if (!_update!(snapshot))
            {
                throw new InvalidOperationException(
                    "The distribution list update did not affect the selected database row.");
            }

            _replace?.Invoke(snapshot);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the distribution list to the database.",
                unchecked((int)0x80004005));
        }
    }

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

    private void Mutate(Func<DistributionListAdministrationSnapshot, DistributionListAdministrationSnapshot> mutation)
    {
        EnsureAuthenticated();
        var snapshot = Snapshot;
        if ((_isAuthenticated is null) ||
            (snapshot.Id == 0 && _insert is null) ||
            (snapshot.Id != 0 && _update is null))
        {
            Unavailable();
            return;
        }

        _list = mutation(snapshot);
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "DistributionList access requires an authenticated server administrator.",
                EAccessDenied);
        }
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

    internal static DistributionLists CreateAuthorizedAdapter(
        int domainId,
        Func<bool>? isAuthenticated = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer distribution-list administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<DistributionListAdministrationSnapshot> LoadLists() => store
            .GetDistributionListsAsync(domainId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertList(DistributionListAdministrationSnapshot list) => store
            .InsertDistributionListAsync(list, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool UpdateList(DistributionListAdministrationSnapshot list) => store
            .UpdateDistributionListAsync(list, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool DeleteList(int distributionListId) => store
            .DeleteDistributionListAsync(domainId, distributionListId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return DistributionLists.CreateAuthorized(
            LoadLists(),
            LoadLists,
            InsertList,
            UpdateList,
            DeleteList,
            isAuthenticated,
            domainId);
    }
}
