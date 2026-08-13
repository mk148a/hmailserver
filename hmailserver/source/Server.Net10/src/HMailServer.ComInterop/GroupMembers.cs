using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("9002BDC6-BCA1-4F37-821C-AE6A70D3046E")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceGroupMembers
{
    [DispId(0)]
    IInterfaceGroupMember this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void DeleteByDBID(int databaseId);

    [DispId(3)]
    IInterfaceGroupMember Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceGroupMember get_ItemByDBID(int databaseId);

    [DispId(6)]
    void Refresh();
}

[ComVisible(true)]
[Guid("EF796379-7192-43CD-B4A5-58E44A4A5B7D")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceGroupMember
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    int GroupID { get; set; }

    [DispId(3)]
    int AccountID { get; set; }

    [DispId(4)]
    void Save();

    [DispId(5)]
    void Delete();

    [DispId(6)]
    IInterfaceAccount Account { get; }
}

[ComVisible(true)]
[Guid("19BD0117-D6EF-49B3-AAC9-9CE70266AEFF")]
[ProgId("hMailServer.GroupMembers.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceGroupMembers))]
public sealed class GroupMembers : IInterfaceGroupMembers
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private GroupMemberAdministrationSnapshot[]? _members;
    private readonly Func<IReadOnlyList<GroupMemberAdministrationSnapshot>>? _reload;
    private readonly int _groupId;
    private readonly Func<GroupMemberAdministrationSnapshot, int>? _insert;
    private readonly Func<GroupMemberAdministrationSnapshot, GroupMemberAdministrationSnapshot>? _saveExisting;
    private readonly Action<int>? _delete;
    private readonly Action<GroupMemberAdministrationSnapshot>? _publish;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;

    public GroupMembers()
    {
    }

    private GroupMembers(
        IReadOnlyList<GroupMemberAdministrationSnapshot> members,
        Func<IReadOnlyList<GroupMemberAdministrationSnapshot>>? reload,
        int groupId,
        Func<GroupMemberAdministrationSnapshot, int>? insert,
        Func<GroupMemberAdministrationSnapshot, GroupMemberAdministrationSnapshot>? saveExisting,
        Action<int>? delete,
        Func<bool>? isServerAdministrator,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory)
    {
        _members = members.ToArray();
        _reload = reload;
        _groupId = groupId;
        _insert = insert;
        _saveExisting = saveExisting;
        _delete = delete;
        _publish = Publish;
        _isServerAdministrator = isServerAdministrator;
        _authorizationLeaseFactory = authorizationLeaseFactory;
    }

    public int Count => GetMembers().Count;

    internal static GroupMembers CreateAuthorized(
        IReadOnlyList<GroupMemberAdministrationSnapshot> members,
        Func<IReadOnlyList<GroupMemberAdministrationSnapshot>>? reload = null,
        int groupId = 0,
        Func<GroupMemberAdministrationSnapshot, int>? insert = null,
        Action<int>? delete = null,
        Func<bool>? isServerAdministrator = null,
        Func<GroupMemberAdministrationSnapshot, GroupMemberAdministrationSnapshot>? saveExisting = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(members);
        return new GroupMembers(
            members,
            reload,
            groupId,
            insert,
            saveExisting,
            delete,
            isServerAdministrator,
            authorizationLeaseFactory);
    }

    private void Publish(GroupMemberAdministrationSnapshot member)
    {
        var current = GetMembers();
        Volatile.Write(ref _members, current.Append(member).ToArray());
    }

    public IInterfaceGroupMember this[int index]
    {
        get
        {
            var members = GetMembers();
            if (index < 0 || index >= members.Count)
            {
                throw new COMException("Group member index was outside the collection.", DispEBadIndex);
            }

            return GroupMember.CreateAuthorized(
                members[index],
                saveExisting: _saveExisting is null ? null : SaveExistingMember,
                delete: _delete is null ? null : DeleteMember,
                ownerGroupId: _groupId,
                isServerAdministrator: _isServerAdministrator,
                authorizationLeaseFactory: _authorizationLeaseFactory);
        }
    }

    public IInterfaceGroupMember get_ItemByDBID(int databaseId)
    {
        var match = GetMembers().FirstOrDefault(member => member.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No group member with the specified database identifier exists.",
                DispEBadIndex)
            : GroupMember.CreateAuthorized(
                match,
                saveExisting: _saveExisting is null ? null : SaveExistingMember,
                delete: _delete is null ? null : DeleteMember,
                ownerGroupId: _groupId,
                isServerAdministrator: _isServerAdministrator,
                authorizationLeaseFactory: _authorizationLeaseFactory);
    }

    public void DeleteByDBID(int databaseId)
    {
        _ = GetMembers();
        EnsureServerAdministrator();
        DeleteMember(databaseId);
    }

    public IInterfaceGroupMember Add()
    {
        _ = GetMembers();
        EnsureServerAdministrator();
        if (_insert is null)
        {
            return Unavailable<IInterfaceGroupMember>();
        }

        return GroupMember.CreateAuthorized(
            new GroupMemberAdministrationSnapshot(Id: 0, GroupId: _groupId, AccountId: 0),
            insert: _insert,
            publish: _publish,
            saveExisting: _saveExisting is null ? null : SaveExistingMember,
            delete: _delete is null ? null : DeleteMember,
            ownerGroupId: _groupId,
            isServerAdministrator: _isServerAdministrator,
            authorizationLeaseFactory: _authorizationLeaseFactory);
    }

    public void Refresh()
    {
        _ = GetMembers();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var members = _reload();
            ArgumentNullException.ThrowIfNull(members);
            Volatile.Write(ref _members, members.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of group members from the database.",
                EFail);
        }
    }

    private IReadOnlyList<GroupMemberAdministrationSnapshot> GetMembers()
    {
        return Volatile.Read(ref _members)
            ?? throw new COMException(
                "GroupMembers access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "GroupMembers access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void DeleteMember(int databaseId)
    {
        var members = GetMembers();
        if (!members.Any(member => member.Id == databaseId))
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
                ref _members,
                members.Where(member => member.Id != databaseId).ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the group member from the database.",
                EFail);
        }
    }

    private GroupMemberAdministrationSnapshot SaveExistingMember(
        GroupMemberAdministrationSnapshot member)
    {
        if (member.GroupId != _groupId)
        {
            throw new COMException(
                "Group member mutation must remain within its owning group.",
                EAccessDenied);
        }

        var members = GetMembers();
        if (!members.Any(existing => existing.Id == member.Id))
        {
            return member;
        }

        if (_saveExisting is null)
        {
            Unavailable();
        }

        try
        {
            var saved = _saveExisting!(member);
            Volatile.Write(
                ref _members,
                members.Select(existing => existing.Id == member.Id ? saved : existing).ToArray());
            return saved;
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the group member to the database.",
                EFail);
        }
    }

    private T Unavailable<T>()
    {
        _ = GetMembers();
        throw new COMException(
            "This GroupMembers member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetMembers();
        throw new COMException(
            "This GroupMembers member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("2AF5F36A-6475-43D3-A037-D31C1FEA7BA8")]
[ProgId("hMailServer.GroupMember.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceGroupMember))]
public sealed class GroupMember : IInterfaceGroupMember
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private GroupMemberAdministrationSnapshot? _member;
    private readonly Func<GroupMemberAdministrationSnapshot, int>? _insert;
    private readonly Func<GroupMemberAdministrationSnapshot, GroupMemberAdministrationSnapshot>? _saveExisting;
    private readonly Action<int>? _delete;
    private readonly Action<GroupMemberAdministrationSnapshot>? _publish;
    private readonly int? _ownerGroupId;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;

    public GroupMember()
    {
    }

    private GroupMember(
        GroupMemberAdministrationSnapshot member,
        Func<GroupMemberAdministrationSnapshot, int>? insert,
        Action<GroupMemberAdministrationSnapshot>? publish,
        Func<GroupMemberAdministrationSnapshot, GroupMemberAdministrationSnapshot>? saveExisting,
        Action<int>? delete,
        int? ownerGroupId,
        Func<bool>? isServerAdministrator,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory)
    {
        _member = member;
        _insert = insert;
        _saveExisting = saveExisting;
        _publish = publish;
        _delete = delete;
        _ownerGroupId = ownerGroupId;
        _isServerAdministrator = isServerAdministrator;
        _authorizationLeaseFactory = authorizationLeaseFactory;
    }

    public int ID => Snapshot.Id;

    public int GroupID
    {
        get => Snapshot.GroupId;
        set
        {
            var snapshot = Snapshot;
            EnsureServerAdministrator();
            if ((snapshot.Id == 0 && _insert is null) ||
                (snapshot.Id != 0 && _saveExisting is null))
            {
                Unavailable();
                return;
            }

            _member = snapshot with { GroupId = value };
        }
    }

    public int AccountID
    {
        get => Snapshot.AccountId;
        set
        {
            var snapshot = Snapshot;
            EnsureServerAdministrator();
            if ((snapshot.Id == 0 && _insert is null) ||
                (snapshot.Id != 0 && _saveExisting is null))
            {
                Unavailable();
                return;
            }

            _member = snapshot with { AccountId = value };
        }
    }

    public IInterfaceAccount Account =>
        AccountAdministrationRuntimeHost.CreateAuthorizedAccountByIdAdapter(
            Snapshot.AccountId,
            _isServerAdministrator,
            _authorizationLeaseFactory);

    internal static GroupMember CreateAuthorized(
        GroupMemberAdministrationSnapshot member,
        Func<GroupMemberAdministrationSnapshot, int>? insert = null,
        Action<GroupMemberAdministrationSnapshot>? publish = null,
        Func<GroupMemberAdministrationSnapshot, GroupMemberAdministrationSnapshot>? saveExisting = null,
        Action<int>? delete = null,
        int? ownerGroupId = null,
        Func<bool>? isServerAdministrator = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null) =>
        new(member, insert, publish, saveExisting, delete, ownerGroupId, isServerAdministrator, authorizationLeaseFactory);

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

        if (_ownerGroupId is null || snapshot.GroupId != _ownerGroupId.Value)
        {
            throw new COMException(
                "Group member mutation must remain within its owning group.",
                EAccessDenied);
        }

        try
        {
            if (snapshot.Id != 0)
            {
                _member = _saveExisting!(snapshot);
                return;
            }

            var insertedId = _insert!(snapshot);
            if (insertedId <= 0)
            {
                throw new InvalidOperationException(
                    "The group member insert did not return a valid generated identity.");
            }

            var saved = snapshot with { Id = insertedId };
            _member = saved;
            _publish?.Invoke(saved);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the group member to the database.",
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

    private GroupMemberAdministrationSnapshot Snapshot =>
        _member ?? throw new COMException(
            "GroupMember access requires an authenticated server administrator.",
            EAccessDenied);

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Group member access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private T Unavailable<T>()
    {
        _ = Snapshot;
        throw new COMException(
            "This GroupMember member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This GroupMember member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class GroupMemberAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IGroupMemberAdministrationStore? _store;

    public static void Configure(IGroupMemberAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static GroupMembers CreateAuthorizedAdapter(
        int groupId,
        Func<bool>? isServerAdministrator = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer group member administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<GroupMemberAdministrationSnapshot> LoadMembers() => store
            .GetGroupMembersAsync(groupId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertMember(GroupMemberAdministrationSnapshot member) => store
            .InsertGroupMemberAsync(member, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void DeleteMember(int memberId)
        {
            if (!store
                .DeleteGroupMemberByIdAsync(groupId, memberId, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult())
            {
                throw new InvalidOperationException(
                    "The group member delete did not affect the selected database row.");
            }
        }

        GroupMemberAdministrationSnapshot UpdateMember(GroupMemberAdministrationSnapshot member)
        {
            if (!store
                .UpdateGroupMemberAsync(groupId, member, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult())
            {
                throw new InvalidOperationException(
                    "The group member update did not affect exactly one owning database row.");
            }

            return member;
        }

        return GroupMembers.CreateAuthorized(
            LoadMembers(),
            LoadMembers,
            groupId,
            InsertMember,
            DeleteMember,
            isServerAdministrator,
            UpdateMember,
            authorizationLeaseFactory);
    }
}
