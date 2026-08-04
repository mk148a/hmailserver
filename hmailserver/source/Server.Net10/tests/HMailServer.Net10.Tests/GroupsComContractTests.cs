using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class GroupsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceGroups),
            "04B3AAAA-2B86-4C71-8A92-2D174055E1F1",
            new[]
            {
                "get_Item", "get_Count", "DeleteByDBID", "Add",
                "get_ItemByDBID", "get_ItemByName", "Refresh"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceGroups).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            7,
            typeof(IInterfaceGroups).GetMethod(nameof(IInterfaceGroups.Refresh))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceGroup),
            "096BA43E-55DA-44BD-A5AD-693DA54222ED",
            new[]
            {
                "get_ID", "get_Name", "set_Name", "get_Members", "Save", "Delete"
            });
        Assert.AreEqual(
            5,
            typeof(IInterfaceGroup).GetProperty(nameof(IInterfaceGroup.Members))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<Groups>(
            "7573CF89-DF41-4079-91B1-894A0DF3E783",
            "hMailServer.Groups.1",
            typeof(IInterfaceGroups));
        AssertComClass<Group>(
            "8F91E8CB-7DE5-494F-92BD-A245D8CC7E15",
            "hMailServer.Group.1",
            typeof(IInterfaceGroup));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var groupsError = Assert.ThrowsExactly<COMException>(() => _ = new Groups().Count);
        var groupsAddError = Assert.ThrowsExactly<COMException>(() => _ = new Groups().Add());
        var groupsRefreshError = Assert.ThrowsExactly<COMException>(new Groups().Refresh);
        var groupError = Assert.ThrowsExactly<COMException>(() => _ = new Group().Name);
        var groupNameSetterError = Assert.ThrowsExactly<COMException>(() => new Group().Name = "Denied");
        var groupSaveError = Assert.ThrowsExactly<COMException>(new Group().Save);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Groups);

        Assert.AreEqual(EAccessDenied, groupsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, groupsAddError.ErrorCode);
        Assert.AreEqual(EAccessDenied, groupsRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, groupError.ErrorCode);
        Assert.AreEqual(EAccessDenied, groupNameSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, groupSaveError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        IInterfaceGroups groups = Groups.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Administrators"),
                Snapshot(20, "Support")
            });

        Assert.AreEqual(2, groups.Count);
        AssertGroup(groups[0], 10, "Administrators");
        AssertGroup(groups.get_ItemByDBID(20), 20, "Support");
        AssertGroup(groups.get_ItemByName("SUPPORT"), 20, "Support");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = groups[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = groups.get_ItemByDBID(30));
        var badName = Assert.ThrowsExactly<COMException>(() => _ = groups.get_ItemByName("Missing"));
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => groups.DeleteByDBID(10));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => groups.Add());
        var pendingRefresh = Assert.ThrowsExactly<COMException>(groups.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => groups[0].Name = "Changed");
        var pendingSave = Assert.ThrowsExactly<COMException>(groups[0].Save);
        var pendingGroupDelete = Assert.ThrowsExactly<COMException>(groups[0].Delete);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingGroupDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceGroups groups = Groups.CreateAuthorized(
            new[]
            {
                Snapshot(10, "Administrators")
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
                    Snapshot(20, "Support"),
                    Snapshot(30, "Operations")
                };
            });

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("Administrators", groups[0].Name);

        groups.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, groups.Count);
        AssertGroup(groups[0], 20, "Support");
        Assert.AreEqual(30, groups.get_ItemByName("OPERATIONS").ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = groups.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(groups.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, groups.Count);
        Assert.AreEqual("Support", groups.get_ItemByDBID(20).Name);
    }

    [TestMethod]
    public void AuthorizedCollection_AddStagesAndPublishesOnlyAfterSuccessfulInsert()
    {
        var failInsert = true;
        var inserted = new List<GroupAdministrationSnapshot>();
        IInterfaceGroups groups = Groups.CreateAuthorized(
            new[] { Snapshot(10, "Administrators") },
            insert: group =>
            {
                inserted.Add(group);
                if (failInsert)
                {
                    throw new InvalidOperationException("Simulated insert failure.");
                }

                return 30;
            });

        var draft = groups.Add();

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(string.Empty, draft.Name);

        draft.Name = "Support";
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual("Support", draft.Name);
        Assert.AreEqual(1, groups.Count);

        var firstSaveFailure = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EFail, firstSaveFailure.ErrorCode);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual("Support", draft.Name);
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(1, inserted.Count);
        Assert.AreEqual(0, inserted[0].Id);
        Assert.AreEqual("Support", inserted[0].Name);

        failInsert = false;
        draft.Save();

        Assert.AreEqual(30, draft.ID);
        Assert.AreEqual("Support", draft.Name);
        Assert.AreEqual(2, groups.Count);
        Assert.AreEqual(30, groups.get_ItemByDBID(30).ID);
        Assert.AreEqual("Support", groups.get_ItemByName("SUPPORT").Name);
        Assert.AreEqual(2, inserted.Count);
    }

    [TestMethod]
    public void RetainedGroupDraft_RechecksLiveServerAdministratorOnSetterAndSave()
    {
        var isServerAdministrator = true;
        var inserted = 0;
        IInterfaceGroups groups = Groups.CreateAuthorized(
            Array.Empty<GroupAdministrationSnapshot>(),
            insert: _ => ++inserted,
            isServerAdministrator: () => isServerAdministrator);

        var draft = groups.Add();
        draft.Name = "Support";
        isServerAdministrator = false;

        var setterError = Assert.ThrowsExactly<COMException>(() => draft.Name = "Denied");
        var saveError = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EAccessDenied, setterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, saveError.ErrorCode);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual("Support", draft.Name);
        Assert.AreEqual(0, inserted);

        isServerAdministrator = true;
        draft.Save();

        Assert.AreEqual(1, draft.ID);
        Assert.AreEqual(1, inserted);
    }

    [TestMethod]
    public void AuthorizedCollection_InvalidGeneratedIdentityFailsWithoutPublishingDraft()
    {
        IInterfaceGroups groups = Groups.CreateAuthorized(
            Array.Empty<GroupAdministrationSnapshot>(),
            insert: _ => 0);
        var draft = groups.Add();
        draft.Name = "Support";

        var saveError = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EFail, saveError.ErrorCode);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual("Support", draft.Name);
        Assert.AreEqual(0, groups.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_ExistingRowSavePublishesOnlyAfterSuccessfulUpdate()
    {
        var allowUpdate = false;
        var updated = new List<GroupAdministrationSnapshot>();
        IInterfaceGroups groups = Groups.CreateAuthorized(
            new[] { Snapshot(10, "Administrators") },
            update: group =>
            {
                updated.Add(group);
                return allowUpdate;
            });
        var group = groups[0];

        group.Name = "Support";
        var saveError = Assert.ThrowsExactly<COMException>(group.Save);

        Assert.AreEqual(EFail, saveError.ErrorCode);
        Assert.AreEqual("Support", group.Name);
        Assert.AreEqual("Administrators", groups[0].Name);
        Assert.AreEqual(1, updated.Count);

        allowUpdate = true;
        group.Save();

        Assert.AreEqual("Support", group.Name);
        Assert.AreEqual("Support", groups[0].Name);
        Assert.AreEqual(2, updated.Count);
    }

    [TestMethod]
    public void RetainedExistingGroup_RechecksLiveServerAdministratorOnSetterAndSave()
    {
        var isServerAdministrator = true;
        var updates = 0;
        IInterfaceGroups groups = Groups.CreateAuthorized(
            new[] { Snapshot(10, "Administrators") },
            update: _ =>
            {
                updates++;
                return true;
            },
            isServerAdministrator: () => isServerAdministrator);
        var group = groups[0];
        isServerAdministrator = false;

        var setterError = Assert.ThrowsExactly<COMException>(() => group.Name = "Denied");
        var saveError = Assert.ThrowsExactly<COMException>(group.Save);

        Assert.AreEqual(EAccessDenied, setterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, saveError.ErrorCode);
        Assert.AreEqual(0, updates);
        Assert.AreEqual("Administrators", group.Name);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredGroupRuntime()
    {
        var store = new MutableGroupAdministrationStore(
            new[]
            {
                Snapshot(20, "Support"),
                Snapshot(10, "Administrators")
            });
        GroupAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var groups = settings.Groups;

        Assert.AreEqual(2, groups.Count);
        Assert.AreEqual("Administrators", groups[0].Name);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(30, "Operations"),
                Snapshot(20, "Support")
            });

        groups.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, groups.Count);
        Assert.AreEqual("Operations", groups[0].Name);
        Assert.AreEqual(30, groups.get_ItemByDBID(30).ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = groups.get_ItemByDBID(10)).ErrorCode);
    }

    private static GroupAdministrationSnapshot Snapshot(int id, string name) => new(id, name);

    private static void AssertGroup(IInterfaceGroup group, int id, string name)
    {
        Assert.AreEqual(id, group.ID);
        Assert.AreEqual(name, group.Name);
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

    private sealed class MutableGroupAdministrationStore(IReadOnlyList<GroupAdministrationSnapshot> groups)
        : IGroupAdministrationStore
    {
        private IReadOnlyList<GroupAdministrationSnapshot> _groups = groups;

        public int ReadCount { get; private set; }

        public void Replace(IReadOnlyList<GroupAdministrationSnapshot> groups)
        {
            _groups = groups;
        }

        public ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> GetGroupsAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<GroupAdministrationSnapshot>>(
                _groups.OrderBy(static group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }

        public ValueTask<int> InsertGroupAsync(
            GroupAdministrationSnapshot group,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
