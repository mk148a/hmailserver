using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapFoldersComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceIMAPFolders),
            "328B16A7-8314-4398-B506-90937569EDBA",
            new[]
            {
                "get_Item", "get_ItemByDBID", "get_ItemByName", "get_Count", "Add", "DeleteByDBID"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceIMAPFolders).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            5,
            typeof(IInterfaceIMAPFolders).GetMethod("DeleteByDBID")?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceIMAPFolder),
            "6EB9E09E-EBE2-4BD7-A8C5-3499257DEB0B",
            new[]
            {
                "get_ID", "get_Name", "set_Name", "get_Subscribed", "set_Subscribed",
                "get_Messages", "get_SubFolders", "Save", "get_ParentID", "get_Permissions",
                "Delete", "get_CurrentUID", "get_CreationTime"
            });
        Assert.AreEqual(
            11,
            typeof(IInterfaceIMAPFolder).GetProperty(nameof(IInterfaceIMAPFolder.CreationTime))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<IMAPFolders>(
            "A0AAF31A-570A-4B78-BDAB-4C33E34BE85F",
            "hMailServer.IMAPFolders.1",
            typeof(IInterfaceIMAPFolders));
        AssertComClass<IMAPFolder>(
            "9FCA085E-E475-4DEE-9D45-5519818DD6E0",
            "hMailServer.IMAPFolder.1",
            typeof(IInterfaceIMAPFolder));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var foldersError = Assert.ThrowsExactly<COMException>(() => _ = new IMAPFolders().Count);
        var folderError = Assert.ThrowsExactly<COMException>(() => _ = new IMAPFolder().Name);

        Assert.AreEqual(EAccessDenied, foldersError.ErrorCode);
        Assert.AreEqual(EAccessDenied, folderError.ErrorCode);
    }

    [TestMethod]
    public void ModifiedUtf7_PreservesLegacyFolderNameExamples()
    {
        Assert.AreEqual("&-", LegacyModifiedUtf7.Encode("&"));
        Assert.AreEqual("&AMU-", LegacyModifiedUtf7.Encode("Å"));
        Assert.AreEqual("TE&AOUA5AD2-ST", LegacyModifiedUtf7.Encode("TEåäöST"));
        Assert.AreEqual("ÄÄÄ", LegacyModifiedUtf7.Decode("&AMQAxADE-"));

        const string greekName = "Ελληνικά";
        Assert.AreEqual(greekName, LegacyModifiedUtf7.Decode(LegacyModifiedUtf7.Encode(greekName)));
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlyRootSnapshotsAndLegacyLookupErrors()
    {
        IInterfaceIMAPFolders folders = IMAPFolders.CreateAuthorized(
            new[]
            {
                new ImapFolderAdministrationSnapshot(
                    10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03"),
                new ImapFolderAdministrationSnapshot(
                    20, 100, -1, "TE&AOUA5AD2-ST", false, 7, "2026-06-26 04:05:06")
            });

        Assert.AreEqual(2, folders.Count);
        AssertFolder(folders[0], 10, -1, "Inbox", true, 42, "2026-06-27 01:02:03");
        AssertFolder(
            folders.get_ItemByDBID(20),
            20,
            -1,
            "TEåäöST",
            false,
            7,
            "2026-06-26 04:05:06");
        Assert.AreEqual(20, folders.get_ItemByName("teåäöst").ID);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = folders[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = folders.get_ItemByDBID(30));
        var badName = Assert.ThrowsExactly<COMException>(() => _ = folders.get_ItemByName("Missing"));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => folders.Add("New"));
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => folders.DeleteByDBID(10));
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => folders[0].Name = "changed");
        var pendingMessages = Assert.ThrowsExactly<COMException>(() => _ = folders[0].Messages);
        var pendingSubFolders = Assert.ThrowsExactly<COMException>(() => _ = folders[0].SubFolders);
        var pendingPermissions = Assert.ThrowsExactly<COMException>(() => _ = folders[0].Permissions);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMessages.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSubFolders.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingPermissions.ErrorCode);
    }

    [TestMethod]
    public void AccountImapFolders_UsesConfiguredRuntimeForSelectedAccount()
    {
        ImapFolderAdministrationRuntimeHost.Configure(
            new FixedImapFolderAdministrationStore(
                new[]
                {
                    new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03"),
                    new ImapFolderAdministrationSnapshot(20, 200, -1, "Outside", true, 1, "2026-06-27 01:02:03")
                }));
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var folders = account.IMAPFolders;

        Assert.AreEqual(1, folders.Count);
        Assert.AreEqual("Inbox", folders[0].Name);
    }

    private static void AssertFolder(
        IInterfaceIMAPFolder folder,
        int id,
        int parentId,
        string name,
        bool subscribed,
        int currentUid,
        string creationTime)
    {
        Assert.AreEqual(id, folder.ID);
        Assert.AreEqual(parentId, folder.ParentID);
        Assert.AreEqual(name, folder.Name);
        Assert.AreEqual(subscribed, folder.Subscribed);
        Assert.AreEqual(currentUid, folder.CurrentUID);
        Assert.AreEqual(creationTime, folder.CreationTime);
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

    private sealed class FixedImapFolderAdministrationStore(
        IReadOnlyList<ImapFolderAdministrationSnapshot> folders) : IImapFolderAdministrationStore
    {
        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                folders.Where(folder => folder.AccountId == accountId && folder.ParentId == -1)
                    .OrderBy(folder => folder.Id)
                    .ToArray());
    }
}
