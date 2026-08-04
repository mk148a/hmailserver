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
    private readonly Action<GroupMemberAdministrationSnapshot>? _publish;
    private readonly Func<bool>? _isServerAdministrator;

    public GroupMembers()
    {
    }

    private GroupMembers(
        IReadOnlyList<GroupMemberAdministrationSnapshot> members,
        Func<IReadOnlyList<GroupMemberAdministrationSnapshot>>? reload,
        int groupId,
        Func<GroupMemberAdministrationSnapshot, int>? insert,
        Func<bool>? isServerAdministrator)
    {
        _members = members.ToArray();
        _reload = reload;
        _groupId = groupId;
        _insert = insert;
        _publish = Publish;
        _isServerAdministrator = isServerAdministrator;
    }

    public int Count => GetMembers().Count;

    internal static GroupMembers CreateAuthorized(
        IReadOnlyList<GroupMemberAdministrationSnapshot> members,
        Func<IReadOnlyList<GroupMemberAdministrationSnapshot>>? reload = null,
        int groupId = 0,
        Func<GroupMemberAdministrationSnapshot, int>? insert = null,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(members);
        return new GroupMembers(members, reload, groupId, insert, isServerAdministrator);
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

            return GroupMember.CreateAuthorized(members[index]);
        }
    }

    public IInterfaceGroupMember get_ItemByDBID(int databaseId)
    {
        var match = GetMembers().FirstOrDefault(member => member.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No group member with the specified database identifier exists.",
                DispEBadIndex)
            : GroupMember.CreateAuthorized(match);
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

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
            ownerGroupId: _groupId,
            isServerAdministrator: _isServerAdministrator);
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
    private readonly Action<GroupMemberAdministrationSnapshot>? _publish;
    private readonly int? _ownerGroupId;
    private readonly Func<bool>? _isServerAdministrator;

    public GroupMember()
    {
    }

    private GroupMember(
        GroupMemberAdministrationSnapshot member,
        Func<GroupMemberAdministrationSnapshot, int>? insert,
        Action<GroupMemberAdministrationSnapshot>? publish,
        int? ownerGroupId,
        Func<bool>? isServerAdministrator)
    {
        _member = member;
        _insert = insert;
        _publish = publish;
        _ownerGroupId = ownerGroupId;
        _isServerAdministrator = isServerAdministrator;
    }

    public int ID => Snapshot.Id;

    public int GroupID
    {
        get => Snapshot.GroupId;
        set
        {
            var snapshot = Snapshot;
            EnsureServerAdministrator();
            if (_insert is null || snapshot.Id != 0)
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
            if (_insert is null || snapshot.Id != 0)
            {
                Unavailable();
                return;
            }

            _member = snapshot with { AccountId = value };
        }
    }

    public IInterfaceAccount Account =>
        AccountAdministrationRuntimeHost.CreateAuthorizedAccountByIdAdapter(Snapshot.AccountId);

    internal static GroupMember CreateAuthorized(
        GroupMemberAdministrationSnapshot member,
        Func<GroupMemberAdministrationSnapshot, int>? insert = null,
        Action<GroupMemberAdministrationSnapshot>? publish = null,
        int? ownerGroupId = null,
        Func<bool>? isServerAdministrator = null) =>
        new(member, insert, publish, ownerGroupId, isServerAdministrator);

    public void Save()
    {
        var snapshot = Snapshot;
        EnsureServerAdministrator();
        if (_insert is null || snapshot.Id != 0)
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
            var insertedId = _insert(snapshot);
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

    public void Delete() => Unavailable();

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
        Func<bool>? isServerAdministrator = null)
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

        return GroupMembers.CreateAuthorized(
            LoadMembers(),
            LoadMembers,
            groupId,
            InsertMember,
            isServerAdministrator);
    }
}
