using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class GroupsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
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
        var groupError = Assert.ThrowsExactly<COMException>(() => _ = new Group().Name);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().Groups);

        Assert.AreEqual(EAccessDenied, groupsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, groupError.ErrorCode);
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
        var pendingMembers = Assert.ThrowsExactly<COMException>(() => _ = groups[0].Members);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => groups[0].Name = "Changed");
        var pendingSave = Assert.ThrowsExactly<COMException>(groups[0].Save);
        var pendingGroupDelete = Assert.ThrowsExactly<COMException>(groups[0].Delete);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMembers.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingGroupDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_UsesConfiguredGroupRuntime()
    {
        GroupAdministrationRuntimeHost.Configure(
            new FixedGroupAdministrationStore(
                new[]
                {
                    Snapshot(20, "Support"),
                    Snapshot(10, "Administrators")
                }));
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var groups = settings.Groups;

        Assert.AreEqual(2, groups.Count);
        Assert.AreEqual("Administrators", groups[0].Name);
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

    private sealed class FixedGroupAdministrationStore(IReadOnlyList<GroupAdministrationSnapshot> groups)
        : IGroupAdministrationStore
    {
        public ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> GetGroupsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<GroupAdministrationSnapshot>>(
                groups.OrderBy(static group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
