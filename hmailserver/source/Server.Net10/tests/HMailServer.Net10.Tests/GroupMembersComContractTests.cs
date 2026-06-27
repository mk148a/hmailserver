using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class GroupMembersComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceGroupMembers),
            "9002BDC6-BCA1-4F37-821C-AE6A70D3046E",
            new[]
            {
                "get_Item", "get_Count", "DeleteByDBID", "Add",
                "get_ItemByDBID", "Refresh"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceGroupMembers).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            6,
            typeof(IInterfaceGroupMembers).GetMethod(nameof(IInterfaceGroupMembers.Refresh))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceGroupMember),
            "EF796379-7192-43CD-B4A5-58E44A4A5B7D",
            new[]
            {
                "get_ID", "get_GroupID", "set_GroupID", "get_AccountID",
                "set_AccountID", "Save", "Delete", "get_Account"
            });
        Assert.AreEqual(
            6,
            typeof(IInterfaceGroupMember).GetProperty(nameof(IInterfaceGroupMember.Account))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<GroupMembers>(
            "19BD0117-D6EF-49B3-AAC9-9CE70266AEFF",
            "hMailServer.GroupMembers.1",
            typeof(IInterfaceGroupMembers));
        AssertComClass<GroupMember>(
            "2AF5F36A-6475-43D3-A037-D31C1FEA7BA8",
            "hMailServer.GroupMember.1",
            typeof(IInterfaceGroupMember));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var membersError = Assert.ThrowsExactly<COMException>(() => _ = new GroupMembers().Count);
        var memberError = Assert.ThrowsExactly<COMException>(() => _ = new GroupMember().GroupID);

        Assert.AreEqual(EAccessDenied, membersError.ErrorCode);
        Assert.AreEqual(EAccessDenied, memberError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        IInterfaceGroupMembers members = GroupMembers.CreateAuthorized(
            new[]
            {
                Snapshot(100, 10, 1000),
                Snapshot(200, 10, 2000)
            });

        Assert.AreEqual(2, members.Count);
        AssertMember(members[0], 100, 10, 1000);
        AssertMember(members.get_ItemByDBID(200), 200, 10, 2000);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = members[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = members.get_ItemByDBID(300));
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => members.DeleteByDBID(100));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => members.Add());
        var pendingRefresh = Assert.ThrowsExactly<COMException>(members.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => members[0].AccountID = 3000);
        var pendingSave = Assert.ThrowsExactly<COMException>(members[0].Save);
        var pendingMemberDelete = Assert.ThrowsExactly<COMException>(members[0].Delete);
        var pendingAccount = Assert.ThrowsExactly<COMException>(() => _ = members[0].Account);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMemberDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAccount.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedGroup_UsesConfiguredGroupMemberRuntime()
    {
        GroupMemberAdministrationRuntimeHost.Configure(
            new FixedGroupMemberAdministrationStore(
                new[]
                {
                    Snapshot(300, 20, 3000),
                    Snapshot(200, 10, 2000),
                    Snapshot(100, 10, 1000)
                }));
        var groups = Groups.CreateAuthorized(new[] { new GroupAdministrationSnapshot(10, "Administrators") });

        var members = groups[0].Members;

        Assert.AreEqual(2, members.Count);
        Assert.AreEqual(100, members[0].ID);
        Assert.AreEqual(1000, members[0].AccountID);
    }

    private static GroupMemberAdministrationSnapshot Snapshot(
        int id,
        int groupId,
        int accountId) =>
        new(id, groupId, accountId);

    private static void AssertMember(
        IInterfaceGroupMember member,
        int id,
        int groupId,
        int accountId)
    {
        Assert.AreEqual(id, member.ID);
        Assert.AreEqual(groupId, member.GroupID);
        Assert.AreEqual(accountId, member.AccountID);
    }

    private static void AssertContract(Type contract, string interfaceId, string[] methodNames)
    {
        Assert.AreEqual(new Guid(interfaceId), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            methodNames,
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());
    }

    private static void AssertComClass<T>(string classId, string progId, Type defaultInterface)
    {
        var type = typeof(T);

        Assert.AreEqual(new Guid(classId), type.GUID);
        Assert.AreEqual(progId, type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(defaultInterface, type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    private sealed class FixedGroupMemberAdministrationStore(
        IReadOnlyList<GroupMemberAdministrationSnapshot> members)
        : IGroupMemberAdministrationStore
    {
        public ValueTask<IReadOnlyList<GroupMemberAdministrationSnapshot>> GetGroupMembersAsync(
            int groupId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<GroupMemberAdministrationSnapshot>>(
                members.Where(member => member.GroupId == groupId)
                    .OrderBy(static member => member.Id)
                    .ToArray());
    }
}
