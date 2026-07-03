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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<GroupAdministrationSnapshot>? _groups;

    public Groups()
    {
    }

    private Groups(IReadOnlyList<GroupAdministrationSnapshot> groups)
    {
        _groups = groups.ToArray();
    }

    public int Count => GetGroups().Count;

    internal static Groups CreateAuthorized(IReadOnlyList<GroupAdministrationSnapshot> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return new Groups(groups);
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

            return Group.CreateAuthorized(groups[index]);
        }
    }

    public IInterfaceGroup get_ItemByDBID(int databaseId)
    {
        var match = GetGroups().FirstOrDefault(group => group.Id == databaseId);

        return match is null
            ? throw new COMException("No group with the specified database identifier exists.", DispEBadIndex)
            : Group.CreateAuthorized(match);
    }

    public IInterfaceGroup get_ItemByName(string name)
    {
        var match = GetGroups().FirstOrDefault(
            group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No group with the specified name exists.", DispEBadIndex)
            : Group.CreateAuthorized(match);
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    public IInterfaceGroup Add() => Unavailable<IInterfaceGroup>();

    public void Refresh() => Unavailable();

    private IReadOnlyList<GroupAdministrationSnapshot> GetGroups()
    {
        return _groups
            ?? throw new COMException(
                "Groups access requires an authenticated server administrator.",
                EAccessDenied);
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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly GroupAdministrationSnapshot? _group;

    public Group()
    {
    }

    private Group(GroupAdministrationSnapshot group)
    {
        _group = group;
    }

    public int ID => Snapshot.Id;

    public string Name { get => Snapshot.Name; set => Unavailable(); }

    public IInterfaceGroupMembers Members =>
        GroupMemberAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id);

    internal static Group CreateAuthorized(GroupAdministrationSnapshot group) => new(group);

    public void Save() => Unavailable();

    public void Delete() => Unavailable();

    private GroupAdministrationSnapshot Snapshot =>
        _group ?? throw new COMException(
            "Group access requires an authenticated server administrator.",
            EAccessDenied);

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

    internal static Groups CreateAuthorizedAdapter()
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

        return Groups.CreateAuthorized(groups);
    }

    internal static Group CreateAuthorizedGroupByIdAdapter(int groupId)
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
            : Group.CreateAuthorized(group);
    }
}
