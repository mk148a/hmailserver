using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DistributionListRecipientsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceDistributionListRecipients),
            "F8759D53-9D91-47EA-A8C2-A9AF151E1FD4",
            new[]
            {
                "get_Item", "get_Count", "get_ItemByDBID", "Add", "DeleteByDBID"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceDistributionListRecipients).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            4,
            typeof(IInterfaceDistributionListRecipients).GetMethod("DeleteByDBID")?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceDistributionListRecipient),
            "6DD90CB4-5E1E-45C8-9748-28A020A13E4D",
            new[]
            {
                "get_ID", "get_RecipientAddress", "set_RecipientAddress", "Delete", "Save"
            });
        Assert.AreEqual(
            2,
            typeof(IInterfaceDistributionListRecipient)
                .GetProperty(nameof(IInterfaceDistributionListRecipient.RecipientAddress))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<DistributionListRecipients>(
            "AB59F3C1-4904-4F1D-883A-4BFC699A7D0B",
            "hMailServer.DistributionListRecipients.1",
            typeof(IInterfaceDistributionListRecipients));
        AssertComClass<DistributionListRecipient>(
            "0886D5D8-4C1C-46F1-BC7B-EEDC9FE9DFFA",
            "hMailServer.DistributionListRecipient.1",
            typeof(IInterfaceDistributionListRecipient));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var recipientsError = Assert.ThrowsExactly<COMException>(() => _ = new DistributionListRecipients().Count);
        var recipientError = Assert.ThrowsExactly<COMException>(() => _ = new DistributionListRecipient().RecipientAddress);

        Assert.AreEqual(EAccessDenied, recipientsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, recipientError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesOnlyReadOnlySnapshotAndLegacyLookupErrors()
    {
        IInterfaceDistributionListRecipients recipients = DistributionListRecipients.CreateAuthorized(
            new[]
            {
                new DistributionListRecipientAdministrationSnapshot(10, 100, "alpha@example.test"),
                new DistributionListRecipientAdministrationSnapshot(20, 100, "beta@example.test")
            });

        Assert.AreEqual(2, recipients.Count);
        AssertRecipient(recipients[0], 10, "alpha@example.test");
        AssertRecipient(recipients.get_ItemByDBID(20), 20, "beta@example.test");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = recipients[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = recipients.get_ItemByDBID(30));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => recipients.Add());
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => recipients[0].RecipientAddress = "changed@example.test");

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
    }

    [TestMethod]
    public void DistributionListRecipients_UsesConfiguredRuntimeForSelectedList()
    {
        DistributionListRecipientAdministrationRuntimeHost.Configure(
            new FixedDistributionListRecipientAdministrationStore(
                new[]
                {
                    new DistributionListRecipientAdministrationSnapshot(10, 100, "alpha@example.test"),
                    new DistributionListRecipientAdministrationSnapshot(20, 200, "outside@example.test")
                }));
        var list = DistributionList.CreateAuthorized(
            new DistributionListAdministrationSnapshot(
                100,
                10,
                "announce@example.test",
                true,
                false,
                string.Empty,
                (int)ComDistributionListMode.Public));

        var recipients = list.Recipients;

        Assert.AreEqual(1, recipients.Count);
        Assert.AreEqual("alpha@example.test", recipients[0].RecipientAddress);
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

    private static void AssertRecipient(
        IInterfaceDistributionListRecipient recipient,
        int id,
        string address)
    {
        Assert.AreEqual(id, recipient.ID);
        Assert.AreEqual(address, recipient.RecipientAddress);
    }

    private sealed class FixedDistributionListRecipientAdministrationStore(
        IReadOnlyList<DistributionListRecipientAdministrationSnapshot> recipients)
        : IDistributionListRecipientAdministrationStore
    {
        public ValueTask<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>> GetRecipientsAsync(
            int distributionListId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>(
                recipients.Where(recipient => recipient.ListId == distributionListId).ToArray());
    }
}
