using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BlockedAttachmentsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceBlockedAttachment),
            "BF5CBCFF-CD54-4FAB-AE60-ADFA9C961C1A",
            new[]
            {
                "get_ID",
                "get_Wildcard",
                "set_Wildcard",
                "Save",
                "get_Description",
                "set_Description",
                "Delete"
            });
        Assert.AreEqual(
            4,
            typeof(IInterfaceBlockedAttachment)
                .GetProperty(nameof(IInterfaceBlockedAttachment.Description))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceBlockedAttachments),
            "8979F461-AD9D-49E8-8068-BBAB43FBA31A",
            new[]
            {
                "get_Item",
                "get_Count",
                "DeleteByDBID",
                "Add",
                "get_ItemByDBID",
                "Refresh"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceBlockedAttachments).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            5,
            typeof(IInterfaceBlockedAttachments)
                .GetMethod("get_ItemByDBID")
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<BlockedAttachments>(
            "1E93E771-45C1-4CAD-9BF6-5D79723C9CBE",
            "hMailServer.BlockedAttachments.1",
            typeof(IInterfaceBlockedAttachments));
        AssertComClass<BlockedAttachment>(
            "773BCF69-C1C2-48CD-A8F8-E89A1F74E4B3",
            "hMailServer.BlockedAttachment.1",
            typeof(IInterfaceBlockedAttachment));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var collectionError = Assert.ThrowsExactly<COMException>(() => _ = new BlockedAttachments().Count);
        var collectionRefreshError = Assert.ThrowsExactly<COMException>(new BlockedAttachments().Refresh);
        var itemError = Assert.ThrowsExactly<COMException>(() => _ = new BlockedAttachment().Wildcard);
        var antiVirusError = Assert.ThrowsExactly<COMException>(() => _ = new AntiVirus().BlockedAttachments);

        Assert.AreEqual(EAccessDenied, collectionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, collectionRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, itemError.ErrorCode);
        Assert.AreEqual(EAccessDenied, antiVirusError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        IInterfaceBlockedAttachments attachments = BlockedAttachments.CreateAuthorized(
            new[]
            {
                Snapshot(10, "*.bat", "Batch file"),
                Snapshot(20, "*.exe", "Executable file")
            });

        Assert.AreEqual(2, attachments.Count);
        AssertAttachment(attachments[0], 10, "*.bat", "Batch file");
        AssertAttachment(attachments.get_ItemByDBID(20), 20, "*.exe", "Executable file");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = attachments[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = attachments.get_ItemByDBID(30));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => attachments.Add());
        var pendingDeleteById = Assert.ThrowsExactly<COMException>(() => attachments.DeleteByDBID(10));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(attachments.Refresh);
        var pendingWildcardMutation = Assert.ThrowsExactly<COMException>(() => attachments[0].Wildcard = "*.cmd");
        var pendingDescriptionMutation = Assert.ThrowsExactly<COMException>(() => attachments[0].Description = "Changed");
        var pendingSave = Assert.ThrowsExactly<COMException>(attachments[0].Save);
        var pendingDelete = Assert.ThrowsExactly<COMException>(attachments[0].Delete);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDeleteById.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingWildcardMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDescriptionMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceBlockedAttachments attachments = BlockedAttachments.CreateAuthorized(
            new[]
            {
                Snapshot(10, "*.bat", "Batch file")
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
                    Snapshot(30, "*.cmd", "Command file"),
                    Snapshot(20, "*.exe", "Executable file")
                };
            });

        Assert.AreEqual(1, attachments.Count);
        Assert.AreEqual("*.bat", attachments[0].Wildcard);

        attachments.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, attachments.Count);
        AssertAttachment(attachments[0], 30, "*.cmd", "Command file");
        Assert.AreEqual("Executable file", attachments.get_ItemByDBID(20).Description);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = attachments.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(attachments.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, attachments.Count);
        Assert.AreEqual("Command file", attachments.get_ItemByDBID(30).Description);
    }

    [TestMethod]
    public void AuthorizedCollection_AddSaveInsertsAndPublishesOnlyNewSnapshot()
    {
        var inserted = new List<BlockedAttachmentAdministrationSnapshot>();
        IInterfaceBlockedAttachments attachments = BlockedAttachments.CreateAuthorized(
            new[] { Snapshot(10, "*.bat", "Batch file") },
            insert: attachment =>
            {
                inserted.Add(attachment);
                return 77;
            },
            isServerAdministrator: static () => true);

        var draft = attachments.Add();
        draft.Wildcard = "*.cmd";
        draft.Description = "Command file";

        Assert.AreEqual(0, draft.ID);
        draft.Save();

        Assert.AreEqual(1, inserted.Count);
        Assert.AreEqual(0, inserted[0].Id);
        Assert.AreEqual("*.cmd", inserted[0].Wildcard);
        Assert.AreEqual("Command file", inserted[0].Description);
        Assert.AreEqual(77, draft.ID);
        Assert.AreEqual(2, attachments.Count);
        AssertAttachment(attachments[1], 77, "*.cmd", "Command file");
    }

    [TestMethod]
    public void AuthorizedCollection_AddSaveFailureRetainsDraftAndParentSnapshot()
    {
        var inserted = 0;
        IInterfaceBlockedAttachments attachments = BlockedAttachments.CreateAuthorized(
            new[] { Snapshot(10, "*.bat", "Batch file") },
            insert: _ =>
            {
                inserted++;
                throw new InvalidOperationException("Simulated insert failure.");
            },
            isServerAdministrator: static () => true);

        var draft = attachments.Add();
        draft.Wildcard = "*.cmd";

        var error = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(1, inserted);
        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(1, attachments.Count);
        AssertAttachment(attachments[0], 10, "*.bat", "Batch file");
    }

    [TestMethod]
    public void AuthorizedCollection_RetainedDraftSaveRechecksServerAdministrator()
    {
        var isServerAdministrator = true;
        var inserts = 0;
        IInterfaceBlockedAttachments attachments = BlockedAttachments.CreateAuthorized(
            Array.Empty<BlockedAttachmentAdministrationSnapshot>(),
            insert: _ =>
            {
                inserts++;
                return 12;
            },
            isServerAdministrator: () => isServerAdministrator);

        var draft = attachments.Add();
        isServerAdministrator = false;

        var error = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(0, inserts);
        Assert.AreEqual(0, attachments.Count);
    }

    [TestMethod]
    public void AuthorizedAntiVirus_UsesConfiguredBlockedAttachmentRuntime()
    {
        var store = new MutableBlockedAttachmentAdministrationStore(
            new[]
            {
                Snapshot(20, "*.exe", "Executable file"),
                Snapshot(10, "*.bat", "Batch file")
            });
        BlockedAttachmentAdministrationRuntimeHost.Configure(
            store);
        var antiVirus = AntiVirus.CreateAuthorized(
            new AntiVirusAdministrationSnapshot(
                ClamWinEnabled: false,
                ClamWinExecutable: string.Empty,
                ClamWinDatabase: string.Empty,
                Action: 0,
                NotifyReceiver: false,
                NotifySender: false,
                CustomScannerEnabled: false,
                CustomScannerExecutable: string.Empty,
                CustomScannerReturnValue: 0,
                MaximumMessageSize: 0,
                EnableAttachmentBlocking: false,
                ClamAvEnabled: false,
                ClamAvHost: string.Empty,
                ClamAvPort: 0));

        var attachments = antiVirus.BlockedAttachments;

        Assert.AreEqual(2, attachments.Count);
        Assert.AreEqual("*.bat", attachments[0].Wildcard);
        Assert.AreEqual("Executable file", attachments.get_ItemByDBID(20).Description);
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(30, "*.cmd", "Command file"),
                Snapshot(20, "*.exe", "Executable file")
            });

        attachments.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, attachments.Count);
        AssertAttachment(attachments[0], 30, "*.cmd", "Command file");
        Assert.AreEqual("Executable file", attachments.get_ItemByDBID(20).Description);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = attachments.get_ItemByDBID(10)).ErrorCode);
    }

    private static BlockedAttachmentAdministrationSnapshot Snapshot(
        int id,
        string wildcard,
        string description) =>
        new(id, wildcard, description);

    private static void AssertAttachment(
        IInterfaceBlockedAttachment attachment,
        int id,
        string wildcard,
        string description)
    {
        Assert.AreEqual(id, attachment.ID);
        Assert.AreEqual(wildcard, attachment.Wildcard);
        Assert.AreEqual(description, attachment.Description);
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

    private sealed class MutableBlockedAttachmentAdministrationStore(
        IReadOnlyList<BlockedAttachmentAdministrationSnapshot> attachments)
        : IBlockedAttachmentAdministrationStore
    {
        private IReadOnlyList<BlockedAttachmentAdministrationSnapshot> _attachments = attachments;

        public int ReadCount { get; private set; }

        public void Replace(IReadOnlyList<BlockedAttachmentAdministrationSnapshot> attachments)
        {
            _attachments = attachments;
        }

        public ValueTask<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>> GetBlockedAttachmentsAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<BlockedAttachmentAdministrationSnapshot>>(
                _attachments.OrderBy(static attachment => attachment.Wildcard, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }
}
