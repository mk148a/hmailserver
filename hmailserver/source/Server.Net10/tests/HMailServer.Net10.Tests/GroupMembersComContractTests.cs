using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class GroupMembersComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
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
        var membersRefreshError = Assert.ThrowsExactly<COMException>(new GroupMembers().Refresh);
        var memberError = Assert.ThrowsExactly<COMException>(() => _ = new GroupMember().GroupID);
        var memberAccountError = Assert.ThrowsExactly<COMException>(() => _ = new GroupMember().Account);

        Assert.AreEqual(EAccessDenied, membersError.ErrorCode);
        Assert.AreEqual(EAccessDenied, membersRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, memberError.ErrorCode);
        Assert.AreEqual(EAccessDenied, memberAccountError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        AccountAdministrationRuntimeHost.Configure(
            new FixedAccountAdministrationStore(
                new[]
                {
                    new AccountAdministrationSnapshot(1000, 10, "member@example.test", true, 0)
                }));

        IInterfaceGroupMembers members = GroupMembers.CreateAuthorized(
            new[]
            {
                Snapshot(100, 10, 1000),
                Snapshot(200, 10, 2000)
            });

        Assert.AreEqual(2, members.Count);
        AssertMember(members[0], 100, 10, 1000);
        AssertMember(members.get_ItemByDBID(200), 200, 10, 2000);
        Assert.AreEqual(1000, members[0].Account.ID);
        Assert.AreEqual("member@example.test", members[0].Account.Address);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = members[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = members.get_ItemByDBID(300));
        var badAccount = Assert.ThrowsExactly<COMException>(() => _ = members.get_ItemByDBID(200).Account);
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => members.DeleteByDBID(100));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => members.Add());
        var pendingRefresh = Assert.ThrowsExactly<COMException>(members.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => members[0].AccountID = 3000);
        var pendingSave = Assert.ThrowsExactly<COMException>(members[0].Save);
        var pendingMemberDelete = Assert.ThrowsExactly<COMException>(members[0].Delete);
        var pendingAccountMutation =
            Assert.ThrowsExactly<COMException>(() => members[0].Account.Address = "changed@example.test");

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badAccount.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMemberDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAccountMutation.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceGroupMembers members = GroupMembers.CreateAuthorized(
            new[]
            {
                Snapshot(100, 10, 1000)
            },
            () =>
            {
                reloads++;
                if (failReload)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }

                return new[]
                {
                    Snapshot(200, 10, 2000),
                    Snapshot(300, 10, 3000)
                };
            });

        Assert.AreEqual(1, members.Count);
        Assert.AreEqual(1000, members[0].AccountID);

        members.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, members.Count);
        AssertMember(members[0], 200, 10, 2000);
        Assert.AreEqual(3000, members.get_ItemByDBID(300).AccountID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = members.get_ItemByDBID(100)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(members.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, members.Count);
        Assert.AreEqual(2000, members.get_ItemByDBID(200).AccountID);
    }

    [TestMethod]
    public void AuthorizedCollection_AddStagesOwnerAndPublishesOnlyAfterSuccessfulInsert()
    {
        var allowInsert = false;
        var inserted = new List<GroupMemberAdministrationSnapshot>();
        IInterfaceGroupMembers members = GroupMembers.CreateAuthorized(
            Array.Empty<GroupMemberAdministrationSnapshot>(),
            groupId: 10,
            insert: member =>
            {
                inserted.Add(member);
                if (!allowInsert)
                {
                    throw new InvalidOperationException("Simulated insert failure.");
                }

                return 300;
            });

        var draft = members.Add();

        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(10, draft.GroupID);
        Assert.AreEqual(0, draft.AccountID);
        draft.AccountID = 1000;
        Assert.AreEqual(0, members.Count);

        var failure = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(1, inserted.Count);
        Assert.AreEqual(10, inserted[0].GroupId);
        Assert.AreEqual(1000, inserted[0].AccountId);
        Assert.AreEqual(0, members.Count);

        allowInsert = true;
        draft.Save();

        Assert.AreEqual(300, draft.ID);
        Assert.AreEqual(1, members.Count);
        AssertMember(members[0], 300, 10, 1000);
    }

    [TestMethod]
    public void NewGroupMember_DeniesCrossParentAndRechecksLiveAdministrator()
    {
        var isServerAdministrator = true;
        var inserts = 0;
        IInterfaceGroupMembers members = GroupMembers.CreateAuthorized(
            Array.Empty<GroupMemberAdministrationSnapshot>(),
            groupId: 10,
            insert: _ => ++inserts,
            isServerAdministrator: () => isServerAdministrator);
        var draft = members.Add();

        draft.GroupID = 11;
        var crossParent = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EAccessDenied, crossParent.ErrorCode);
        Assert.AreEqual(0, inserts);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(0, members.Count);

        draft.GroupID = 10;
        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(() => draft.AccountID = 1000);
        var deniedSave = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(EAccessDenied, deniedSave.ErrorCode);
        Assert.AreEqual(0, inserts);
    }

    [TestMethod]
    public void AuthorizedCollection_GroupMemberDeletePublishesOnlyAfterSuccessfulDelete()
    {
        var allowDelete = false;
        var deleted = new List<int>();
        IInterfaceGroupMembers members = GroupMembers.CreateAuthorized(
            new[] { Snapshot(100, 10, 1000), Snapshot(200, 10, 2000) },
            groupId: 10,
            delete: memberId =>
            {
                deleted.Add(memberId);
                if (!allowDelete)
                {
                    throw new InvalidOperationException("Simulated delete failure.");
                }
            });
        var member = members.get_ItemByDBID(100);

        var failure = Assert.ThrowsExactly<COMException>(member.Delete);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(2, members.Count);
        Assert.AreEqual(1, deleted.Count);

        allowDelete = true;
        member.Delete();

        Assert.AreEqual(1, members.Count);
        Assert.AreEqual(200, members[0].ID);
        Assert.AreEqual(100, deleted[1]);

        member.Delete();
        Assert.AreEqual(2, deleted.Count);
    }

    [TestMethod]
    public void GroupMemberDelete_RechecksLiveAdministratorAndScopesUnknownIds()
    {
        var isServerAdministrator = true;
        var deletes = 0;
        IInterfaceGroupMembers members = GroupMembers.CreateAuthorized(
            new[] { Snapshot(100, 10, 1000) },
            groupId: 10,
            delete: _ => deletes++,
            isServerAdministrator: () => isServerAdministrator);
        var member = members[0];

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(member.Delete);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, deletes);

        isServerAdministrator = true;
        members.DeleteByDBID(999);
        Assert.AreEqual(0, deletes);
    }

    [TestMethod]
    public void ExistingGroupMemberSavePublishesOnlyAfterSuccessfulOwnerScopedUpdate()
    {
        var allowUpdate = false;
        var updated = new List<GroupMemberAdministrationSnapshot>();
        IInterfaceGroupMembers members = GroupMembers.CreateAuthorized(
            new[] { Snapshot(100, 10, 1000) },
            groupId: 10,
            saveExisting: member =>
            {
                updated.Add(member);
                if (!allowUpdate)
                {
                    throw new InvalidOperationException("Simulated update failure.");
                }

                return member;
            });
        var member = members[0];

        member.AccountID = 2000;
        var failure = Assert.ThrowsExactly<COMException>(member.Save);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(2000, member.AccountID);
        Assert.AreEqual(1000, members[0].AccountID);
        Assert.AreEqual(1, updated.Count);
        Assert.AreEqual(2000, updated[0].AccountId);

        allowUpdate = true;
        member.Save();

        Assert.AreEqual(2000, member.AccountID);
        Assert.AreEqual(2000, members[0].AccountID);
        Assert.AreEqual(2, updated.Count);
    }

    [TestMethod]
    public void ExistingGroupMemberMutation_RechecksAdministratorAndRejectsCrossParentSave()
    {
        var isServerAdministrator = true;
        var updates = 0;
        IInterfaceGroupMembers members = GroupMembers.CreateAuthorized(
            new[] { Snapshot(100, 10, 1000) },
            groupId: 10,
            saveExisting: member =>
            {
                updates++;
                return member;
            },
            isServerAdministrator: () => isServerAdministrator);
        var member = members[0];

        isServerAdministrator = false;
        var setterError = Assert.ThrowsExactly<COMException>(() => member.AccountID = 2000);
        var saveError = Assert.ThrowsExactly<COMException>(member.Save);

        Assert.AreEqual(EAccessDenied, setterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, saveError.ErrorCode);
        Assert.AreEqual(0, updates);

        isServerAdministrator = true;
        member.GroupID = 11;
        var crossParent = Assert.ThrowsExactly<COMException>(member.Save);

        Assert.AreEqual(EAccessDenied, crossParent.ErrorCode);
        Assert.AreEqual(0, updates);
        Assert.AreEqual(10, members[0].GroupID);
    }

    [TestMethod]
    public void AuthorizedGroup_UsesConfiguredGroupMemberRuntime()
    {
        AccountAdministrationRuntimeHost.Configure(
            new FixedAccountAdministrationStore(
                new[]
                {
                    new AccountAdministrationSnapshot(1000, 10, "member@example.test", true, 0),
                    new AccountAdministrationSnapshot(2000, 10, "refreshed@example.test", true, 0),
                    new AccountAdministrationSnapshot(3000, 10, "second@example.test", true, 0)
                }));
        var store = new MutableGroupMemberAdministrationStore(
            new[]
            {
                Snapshot(300, 20, 3000),
                Snapshot(200, 10, 2000),
                Snapshot(100, 10, 1000)
            });
        GroupMemberAdministrationRuntimeHost.Configure(
            store);
        var groups = Groups.CreateAuthorized(new[] { new GroupAdministrationSnapshot(10, "Administrators") });

        var members = groups[0].Members;

        Assert.AreEqual(2, members.Count);
        Assert.AreEqual(100, members[0].ID);
        Assert.AreEqual(1000, members[0].AccountID);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(500, 20, 1000),
                Snapshot(400, 10, 3000),
                Snapshot(150, 10, 2000)
            });

        members.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, members.Count);
        AssertMember(members[0], 150, 10, 2000);
        Assert.AreEqual(3000, members.get_ItemByDBID(400).Account.ID);
        Assert.AreEqual("second@example.test", members.get_ItemByDBID(400).Account.Address);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = members.get_ItemByDBID(100)).ErrorCode);
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

    private sealed class FixedAccountAdministrationStore(IReadOnlyList<AccountAdministrationSnapshot> accounts)
        : IAccountAdministrationStore
    {
        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AccountAdministrationSnapshot>>(
                accounts.Where(account => account.DomainId == domainId).ToArray());

        public ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(accounts.FirstOrDefault(account => account.Id == accountId));
    }

    private sealed class MutableGroupMemberAdministrationStore(
        IReadOnlyList<GroupMemberAdministrationSnapshot> members)
        : IGroupMemberAdministrationStore
    {
        private IReadOnlyList<GroupMemberAdministrationSnapshot> _members = members;

        public int ReadCount { get; private set; }

        public void Replace(IReadOnlyList<GroupMemberAdministrationSnapshot> members)
        {
            _members = members;
        }

        public ValueTask<IReadOnlyList<GroupMemberAdministrationSnapshot>> GetGroupMembersAsync(
            int groupId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<GroupMemberAdministrationSnapshot>>(
                _members.Where(member => member.GroupId == groupId)
                    .OrderBy(static member => member.Id)
                    .ToArray());
        }
    }
}
