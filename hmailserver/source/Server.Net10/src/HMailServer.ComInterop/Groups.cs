using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("04B3AAAA-2B86-4C71-8A92-2D174055E1F1")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceGroups
{
    [DispId(0)]
    IInterfaceGroup this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceGroup Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceGroup get_ItemByDBID(int databaseId);

    [DispId(6)]
    [SpecialName]
    IInterfaceGroup get_ItemByName([MarshalAs(UnmanagedType.BStr)] string name);

    [DispId(7)]
    void Refresh();
}

[ComVisible(true)]
[Guid("096BA43E-55DA-44BD-A5AD-693DA54222ED")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceGroup
{
    [DispId(1)]
    int ID { get; }

    [DispId(4)]
    string Name { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(5)]
    IInterfaceGroupMembers Members { get; }

    [DispId(7)]
    void Save();

    [DispId(8)]
    void Delete();
}

[ComVisible(true)]
[Guid("7573CF89-DF41-4079-91B1-894A0DF3E783")]
[ProgId("hMailServer.Groups.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceGroups))]
public sealed class Groups : IInterfaceGroups
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private GroupAdministrationSnapshot[]? _groups;
    private readonly Func<IReadOnlyList<GroupAdministrationSnapshot>>? _reload;
    private readonly Func<GroupAdministrationSnapshot, int>? _insert;
    private readonly Func<GroupAdministrationSnapshot, bool>? _update;
    private readonly Action<int>? _delete;
    private readonly Func<bool>? _isServerAdministrator;

    public Groups()
    {
    }

    private Groups(
        IReadOnlyList<GroupAdministrationSnapshot> groups,
        Func<IReadOnlyList<GroupAdministrationSnapshot>>? reload,
        Func<GroupAdministrationSnapshot, int>? insert,
        Func<bool>? isServerAdministrator,
        Func<GroupAdministrationSnapshot, bool>? update,
        Action<int>? delete)
    {
        _groups = groups.ToArray();
        _reload = reload;
        _insert = insert;
        _isServerAdministrator = isServerAdministrator;
        _update = update;
        _delete = delete;
    }

    public int Count => GetGroups().Count;

    internal static Groups CreateAuthorized(
        IReadOnlyList<GroupAdministrationSnapshot> groups,
        Func<IReadOnlyList<GroupAdministrationSnapshot>>? reload = null,
        Func<GroupAdministrationSnapshot, int>? insert = null,
        Func<bool>? isServerAdministrator = null,
        Func<GroupAdministrationSnapshot, bool>? update = null,
        Action<int>? delete = null)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return new Groups(groups, reload, insert, isServerAdministrator, update, delete);
    }

    public IInterfaceGroup this[int index]
    {
        get
        {
            var groups = GetGroups();
            if (index < 0 || index >= groups.Count)
            {
                throw new COMException("Group index was outside the collection.", DispEBadIndex);
            }

            return Group.CreateAuthorized(
                groups[index],
                saveExisting: _update is null ? null : SaveExistingGroup,
                delete: _delete is null ? null : DeleteExistingGroup,
                isServerAdministrator: _isServerAdministrator);
        }
    }

    public IInterfaceGroup get_ItemByDBID(int databaseId)
    {
        var match = GetGroups().FirstOrDefault(group => group.Id == databaseId);

        return match is null
            ? throw new COMException("No group with the specified database identifier exists.", DispEBadIndex)
            : Group.CreateAuthorized(
                match,
                saveExisting: _update is null ? null : SaveExistingGroup,
                delete: _delete is null ? null : DeleteExistingGroup,
                isServerAdministrator: _isServerAdministrator);
    }

    public IInterfaceGroup get_ItemByName(string name)
    {
        var match = GetGroups().FirstOrDefault(
            group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No group with the specified name exists.", DispEBadIndex)
            : Group.CreateAuthorized(
                match,
                saveExisting: _update is null ? null : SaveExistingGroup,
                delete: _delete is null ? null : DeleteExistingGroup,
                isServerAdministrator: _isServerAdministrator);
    }

    public void DeleteByDBID(int databaseId)
    {
        _ = GetGroups();
        EnsureServerAdministrator();
        DeleteExistingGroup(databaseId);
    }

    public IInterfaceGroup Add()
    {
        _ = GetGroups();
        EnsureServerAdministrator();
        if (_insert is null)
        {
            return Unavailable<IInterfaceGroup>();
        }

        return Group.CreateAuthorized(
            new GroupAdministrationSnapshot(Id: 0, Name: string.Empty),
            insert: _insert,
            publish: Publish,
            isServerAdministrator: _isServerAdministrator);
    }

    public void Refresh()
    {
        _ = GetGroups();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var groups = _reload();
            ArgumentNullException.ThrowIfNull(groups);
            Volatile.Write(ref _groups, groups.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of groups from the database.",
                EFail);
        }
    }

    private IReadOnlyList<GroupAdministrationSnapshot> GetGroups()
    {
        return Volatile.Read(ref _groups)
            ?? throw new COMException(
                "Groups access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Groups access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void Publish(GroupAdministrationSnapshot group)
    {
        var groups = GetGroups();
        Volatile.Write(ref _groups, groups.Append(group).ToArray());
    }

    private GroupAdministrationSnapshot SaveExistingGroup(GroupAdministrationSnapshot group)
    {
        if (_update is null)
        {
            Unavailable();
        }

        try
        {
            if (!_update!(group))
            {
                throw new InvalidOperationException(
                    "The group update did not affect exactly one row.");
            }

            var groups = GetGroups();
            Volatile.Write(
                ref _groups,
                groups.Select(existing => existing.Id == group.Id ? group : existing).ToArray());
            return group;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the group to the database.",
                EFail);
        }
    }

    private void DeleteExistingGroup(int databaseId)
    {
        var groups = GetGroups();
        if (!groups.Any(group => group.Id == databaseId))
        {
            return;
        }

        if (_delete is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _delete(databaseId);
            Volatile.Write(
                ref _groups,
                groups.Where(group => group.Id != databaseId).ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the group from the database.",
                EFail);
        }
    }

    private T Unavailable<T>()
    {
        _ = GetGroups();
        throw new COMException(
            "This Groups member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetGroups();
        throw new COMException(
            "This Groups member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("8F91E8CB-7DE5-494F-92BD-A245D8CC7E15")]
[ProgId("hMailServer.Group.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceGroup))]
public sealed class Group : IInterfaceGroup
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private GroupAdministrationSnapshot? _group;
    private readonly Func<GroupAdministrationSnapshot, int>? _insert;
    private readonly Action<GroupAdministrationSnapshot>? _publish;
    private readonly Func<GroupAdministrationSnapshot, GroupAdministrationSnapshot>? _saveExisting;
    private readonly Action<int>? _delete;
    private readonly Func<bool>? _isServerAdministrator;

    public Group()
    {
    }

    private Group(
        GroupAdministrationSnapshot group,
        Func<GroupAdministrationSnapshot, int>? insert,
        Action<GroupAdministrationSnapshot>? publish,
        Func<bool>? isServerAdministrator,
        Func<GroupAdministrationSnapshot, GroupAdministrationSnapshot>? saveExisting,
        Action<int>? delete)
    {
        _group = group;
        _insert = insert;
        _publish = publish;
        _isServerAdministrator = isServerAdministrator;
        _saveExisting = saveExisting;
        _delete = delete;
    }

    public int ID => Snapshot.Id;

    public string Name
    {
        get => Snapshot.Name;
        set
        {
            var snapshot = Snapshot;
            EnsureServerAdministrator();
            if ((snapshot.Id == 0 && _insert is null) || (snapshot.Id != 0 && _saveExisting is null))
            {
                Unavailable();
                return;
            }

            _group = snapshot with { Name = value ?? string.Empty };
        }
    }

    public IInterfaceGroupMembers Members =>
        Snapshot.Id == 0
            ? Unavailable<IInterfaceGroupMembers>()
            : GroupMemberAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id, _isServerAdministrator);

    internal static Group CreateAuthorized(
        GroupAdministrationSnapshot group,
        Func<GroupAdministrationSnapshot, int>? insert = null,
        Action<GroupAdministrationSnapshot>? publish = null,
        Func<bool>? isServerAdministrator = null,
        Func<GroupAdministrationSnapshot, GroupAdministrationSnapshot>? saveExisting = null,
        Action<int>? delete = null) =>
        new(group, insert, publish, isServerAdministrator, saveExisting, delete);

    public void Save()
    {
        var snapshot = Snapshot;
        EnsureServerAdministrator();
        if ((snapshot.Id == 0 && _insert is null) ||
            (snapshot.Id != 0 && _saveExisting is null))
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
                        "The group insert did not return a valid generated identity.");
                }

                var saved = snapshot with { Id = insertedId };
                _group = saved;
                _publish?.Invoke(saved);
            }
            else
            {
                _group = _saveExisting!(snapshot);
            }
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the group to the database.",
                EFail);
        }
    }

    public void Delete()
    {
        EnsureServerAdministrator();
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete(Snapshot.Id);
    }

    private GroupAdministrationSnapshot Snapshot =>
        _group ?? throw new COMException(
            "Group access requires an authenticated server administrator.",
            EAccessDenied);

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Group access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private T Unavailable<T>()
    {
        _ = Snapshot;
        throw new COMException(
            "This Group member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This Group member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class GroupAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    private static IGroupAdministrationStore? _store;

    public static void Configure(IGroupAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Groups CreateAuthorizedAdapter(Func<bool>? isServerAdministrator = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer group administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<GroupAdministrationSnapshot> LoadGroups() => store
            .GetGroupsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertGroup(GroupAdministrationSnapshot group) => store
            .InsertGroupAsync(group, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        bool UpdateGroup(GroupAdministrationSnapshot group) => store
            .UpdateGroupAsync(group, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void DeleteGroup(int groupId)
        {
            if (!store
                .DeleteGroupByIdAsync(groupId, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult())
            {
                throw new InvalidOperationException(
                    "The group delete did not affect the selected database row.");
            }
        }

        return Groups.CreateAuthorized(
            LoadGroups(),
            LoadGroups,
            InsertGroup,
            isServerAdministrator,
            UpdateGroup,
            DeleteGroup);
    }

    internal static Group CreateAuthorizedGroupByIdAdapter(
        int groupId,
        Func<bool>? isServerAdministrator = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer group administration runtime has not been initialized.",
                CoENotInitialized);

        var groups = store
            .GetGroupsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        var group = groups.FirstOrDefault(item => item.Id == groupId);

        return group is null
            ? throw new COMException("No group with the specified database identifier exists.", DispEBadIndex)
            : Group.CreateAuthorized(group, isServerAdministrator: isServerAdministrator);
    }
}
