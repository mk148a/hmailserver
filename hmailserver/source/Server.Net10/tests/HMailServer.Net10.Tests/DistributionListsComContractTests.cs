using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DistributionListsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceDistributionLists),
            "8F0E22B8-0824-42DF-9260-F8B9ABFA8C61",
            new[]
            {
                "get_Item", "get_Count", "get_ItemByDBID", "Add", "DeleteByDBID",
                "get_ItemByAddress", "Refresh"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceDistributionLists).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            5,
            typeof(IInterfaceDistributionLists).GetMethod("get_ItemByAddress")?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceDistributionList),
            "8251393D-27D8-4DF2-8A05-949C11D42C09",
            new[]
            {
                "get_ID", "Delete", "Save", "get_Active", "set_Active", "get_Recipients",
                "get_Address", "set_Address", "get_RequireSMTPAuth", "set_RequireSMTPAuth",
                "get_RequireSenderAddress", "set_RequireSenderAddress", "get_Mode", "set_Mode"
            });
        Assert.AreEqual(
            10,
            typeof(IInterfaceDistributionList).GetProperty(nameof(IInterfaceDistributionList.Mode))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<DistributionLists>(
            "C3DD0A4A-0551-442F-859A-76AAB92A6CF1",
            "hMailServer.DistributionLists.1",
            typeof(IInterfaceDistributionLists));
        AssertComClass<DistributionList>(
            "990D27ED-86CE-4DCB-B1C1-1E130C07F918",
            "hMailServer.DistributionList.1",
            typeof(IInterfaceDistributionList));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var listsError = Assert.ThrowsExactly<COMException>(() => _ = new DistributionLists().Count);
        var listError = Assert.ThrowsExactly<COMException>(() => _ = new DistributionList().Address);

        Assert.AreEqual(EAccessDenied, listsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, listError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesOnlyReadOnlySnapshotAndLegacyLookupErrors()
    {
        IInterfaceDistributionLists lists = DistributionLists.CreateAuthorized(
            new[]
            {
                new DistributionListAdministrationSnapshot(
                    10,
                    100,
                    "announce@example.test",
                    true,
                    false,
                    string.Empty,
                    (int)ComDistributionListMode.Public),
                new DistributionListAdministrationSnapshot(
                    20,
                    100,
                    "members@example.test",
                    false,
                    true,
                    "owner@example.test",
                    (int)ComDistributionListMode.Membership)
            });

        Assert.AreEqual(2, lists.Count);
        AssertDistributionList(
            lists[0],
            10,
            "announce@example.test",
            true,
            false,
            string.Empty,
            ComDistributionListMode.Public);
        AssertDistributionList(
            lists.get_ItemByAddress("MEMBERS@EXAMPLE.TEST"),
            20,
            "members@example.test",
            false,
            true,
            "owner@example.test",
            ComDistributionListMode.Membership);
        AssertDistributionList(
            lists.get_ItemByDBID(10),
            10,
            "announce@example.test",
            true,
            false,
            string.Empty,
            ComDistributionListMode.Public);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = lists[2]);
        var badAddress = Assert.ThrowsExactly<COMException>(() => _ = lists.get_ItemByAddress("missing@example.test"));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(lists.Refresh);
        var pendingRecipients = Assert.ThrowsExactly<COMException>(() => _ = lists[0].Recipients);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => lists[0].Address = "changed@example.test");

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badAddress.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRecipients.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
    }

    [TestMethod]
    public void DomainDistributionLists_UsesConfiguredRuntimeForSelectedDomain()
    {
        DistributionListAdministrationRuntimeHost.Configure(
            new FixedDistributionListAdministrationStore(
                new[]
                {
                    new DistributionListAdministrationSnapshot(
                        10,
                        100,
                        "announce@example.test",
                        true,
                        false,
                        string.Empty,
                        (int)ComDistributionListMode.Public),
                    new DistributionListAdministrationSnapshot(
                        20,
                        200,
                        "outside@example.test",
                        true,
                        false,
                        string.Empty,
                        (int)ComDistributionListMode.Public)
                }));
        var domain = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true));

        var lists = domain.DistributionLists;

        Assert.AreEqual(1, lists.Count);
        Assert.AreEqual("announce@example.test", lists[0].Address);
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

    private static void AssertDistributionList(
        IInterfaceDistributionList list,
        int id,
        string address,
        bool active,
        bool requireSmtpAuth,
        string requireSenderAddress,
        ComDistributionListMode mode)
    {
        Assert.AreEqual(id, list.ID);
        Assert.AreEqual(address, list.Address);
        Assert.AreEqual(active, list.Active);
        Assert.AreEqual(requireSmtpAuth, list.RequireSMTPAuth);
        Assert.AreEqual(requireSenderAddress, list.RequireSenderAddress);
        Assert.AreEqual(mode, list.Mode);
    }

    private sealed class FixedDistributionListAdministrationStore(
        IReadOnlyList<DistributionListAdministrationSnapshot> lists)
        : IDistributionListAdministrationStore
    {
        public ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DistributionListAdministrationSnapshot>>(
                lists.Where(list => list.DomainId == domainId).ToArray());
    }
}
