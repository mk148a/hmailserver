using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ImapFoldersComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ELegacyComError = unchecked((int)0x800403E9);
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
        var addError = Assert.ThrowsExactly<COMException>(() => new IMAPFolders().Add("New"));
        var folderError = Assert.ThrowsExactly<COMException>(() => _ = new IMAPFolder().Name);
        var folderSetterError = Assert.ThrowsExactly<COMException>(() => new IMAPFolder().Name = "New");
        var folderSubscribedSetterError = Assert.ThrowsExactly<COMException>(() => new IMAPFolder().Subscribed = false);
        var folderSaveError = Assert.ThrowsExactly<COMException>(() => new IMAPFolder().Save());
        var folderDeleteError = Assert.ThrowsExactly<COMException>(() => new IMAPFolder().Delete());
        var accountFoldersError = Assert.ThrowsExactly<COMException>(() => _ = new Account().IMAPFolders);

        Assert.AreEqual(EAccessDenied, foldersError.ErrorCode);
        Assert.AreEqual(EAccessDenied, addError.ErrorCode);
        Assert.AreEqual(EAccessDenied, folderError.ErrorCode);
        Assert.AreEqual(EAccessDenied, folderSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, folderSubscribedSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, folderSaveError.ErrorCode);
        Assert.AreEqual(EAccessDenied, folderDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, accountFoldersError.ErrorCode);
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
        MessageAdministrationRuntimeHost.Configure(new EmptyMessageAdministrationStore());
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
        var pendingSubscribedMutation = Assert.ThrowsExactly<COMException>(() => folders[0].Subscribed = false);
        var pendingItemDelete = Assert.ThrowsExactly<COMException>(() => folders[0].Delete());
        var messages = folders[0].Messages;
        var pendingPermissions = Assert.ThrowsExactly<COMException>(() => _ = folders[0].Permissions);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSubscribedMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingItemDelete.ErrorCode);
        Assert.AreEqual(0, messages.Count);
        Assert.AreEqual(ELegacyComError, pendingPermissions.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedFolderSubFolders_UsesConfiguredRuntimeForSelectedParentAndAccount()
    {
        ImapFolderAdministrationRuntimeHost.Configure(
            new FixedImapFolderAdministrationStore(
                new[]
                {
                    new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03"),
                    new ImapFolderAdministrationSnapshot(11, 100, 10, "Child", true, 8, "2026-06-27 02:03:04"),
                    new ImapFolderAdministrationSnapshot(12, 100, 10, "Later", false, 9, "2026-06-27 03:04:05"),
                    new ImapFolderAdministrationSnapshot(20, 100, -1, "Archive", true, 2, "2026-06-27 04:05:06"),
                    new ImapFolderAdministrationSnapshot(30, 200, 10, "OtherAccount", true, 1, "2026-06-27 05:06:07")
                }));
        IInterfaceIMAPFolders folders = IMAPFolders.CreateAuthorized(
            new[]
            {
                new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03")
            });

        var subFolders = folders[0].SubFolders;

        Assert.AreEqual(2, subFolders.Count);
        AssertFolder(subFolders[0], 11, 10, "Child", true, 8, "2026-06-27 02:03:04");
        AssertFolder(subFolders.get_ItemByDBID(12), 12, 10, "Later", false, 9, "2026-06-27 03:04:05");
        var outsideAccount = Assert.ThrowsExactly<COMException>(() => _ = subFolders.get_ItemByDBID(30));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => subFolders.Add("New child"));
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => subFolders.DeleteByDBID(11));

        Assert.AreEqual(DispEBadIndex, outsideAccount.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
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

    [TestMethod]
    public void AuthorizedAccountImapFolders_AddInsertsAndAppendsOnlyToOwningSnapshot()
    {
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03"),
                new ImapFolderAdministrationSnapshot(30, 200, -1, "Other", true, 1, "2026-06-27 01:02:03")
            });
        ImapFolderAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var folders = account.IMAPFolders;
        var added = folders.Add("Projects");

        Assert.AreEqual(2, folders.Count);
        Assert.AreEqual(101, added.ID);
        Assert.AreEqual("Projects", added.Name);
        Assert.AreEqual(-1, added.ParentID);
        Assert.IsFalse(added.Subscribed);
        Assert.AreEqual(100, store.LastInsertAccountId);
        Assert.AreEqual(-1, store.LastInsertParentId);
        Assert.AreEqual("Projects", store.LastInsertName);
        Assert.AreEqual(1, store.InsertCount);
        Assert.AreEqual(101, folders.get_ItemByName("Projects").ID);
        Assert.AreEqual(101, folders.get_ItemByDBID(101).ID);
        var duplicateError = Assert.ThrowsExactly<COMException>(() => folders.Add("projects"));
        Assert.AreEqual(ELegacyComError, duplicateError.ErrorCode);
        Assert.AreEqual(1, store.InsertCount);

        store.ReturnMisScopedInsert = true;
        var misScopedError = Assert.ThrowsExactly<COMException>(() => folders.Add("Other"));
        Assert.AreEqual(ELegacyComError, misScopedError.ErrorCode);
        Assert.AreEqual(2, store.InsertCount);
        Assert.AreEqual(2, folders.Count);
    }

    [TestMethod]
    public void AuthorizedImapFolder_NameStagesUtf7AndSaveReplacesSharedSnapshot()
    {
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03")
            });
        ImapFolderAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var folder = account.IMAPFolders.get_ItemByDBID(10);

        folder.Name = "TEåäöST";

        Assert.AreEqual("TEåäöST", folder.Name);
        Assert.AreEqual(0, store.UpdateCount);
        folder.Save();

        Assert.AreEqual(1, store.UpdateCount);
        Assert.AreEqual("TE&AOUA5AD2-ST", store.LastUpdatedFolder!.Name);
        Assert.AreEqual("TEåäöST", account.IMAPFolders.get_ItemByDBID(10).Name);
        Assert.AreEqual(1, account.IMAPFolders.Count);
    }

    [TestMethod]
    public void AuthorizedImapFolder_SaveFailureLeavesSharedSnapshotUnchanged()
    {
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03")
            })
        {
            ReturnUpdateSuccess = false
        };
        ImapFolderAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var folder = account.IMAPFolders.get_ItemByDBID(10);
        folder.Name = "Broken";

        var error = Assert.ThrowsExactly<COMException>(() => folder.Save());

        Assert.AreEqual(ELegacyComError, error.ErrorCode);
        Assert.AreEqual("Inbox", account.IMAPFolders.get_ItemByDBID(10).Name);
        Assert.AreEqual(1, account.IMAPFolders.Count);
    }

    [TestMethod]
    public void AuthorizedImapFolder_SubscribedStagesAndSaveReplacesSharedSnapshot()
    {
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03")
            });
        ImapFolderAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var folder = account.IMAPFolders.get_ItemByDBID(10);

        folder.Subscribed = false;

        Assert.IsFalse(folder.Subscribed);
        Assert.AreEqual(0, store.UpdateCount);
        folder.Save();

        Assert.AreEqual(1, store.UpdateCount);
        Assert.IsFalse(store.LastUpdatedFolder!.Subscribed);
        Assert.IsFalse(account.IMAPFolders.get_ItemByDBID(10).Subscribed);
        Assert.AreEqual(1, account.IMAPFolders.Count);
    }

    [TestMethod]
    public void AuthorizedImapFolder_DeleteRemovesSubtreeAndPreservesRootInbox()
    {
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03"),
                new ImapFolderAdministrationSnapshot(11, 100, 10, "Child", true, 8, "2026-06-27 02:03:04"),
                new ImapFolderAdministrationSnapshot(12, 100, 11, "Nested", false, 3, "2026-06-27 03:04:05"),
                new ImapFolderAdministrationSnapshot(20, 100, -1, "Archive", true, 2, "2026-06-27 04:05:06")
            })
        {
            DeletedMessages = new[]
            {
                new ImapFolderAdministrationDeletedMessage(
                    "child.eml", 100, 11, "admin@example.test", 1)
            }
        };
        var fileDeletion = new RecordingMessageFileDeletionRuntime { ReturnSuccess = false };
        ImapFolderAdministrationRuntimeHost.Configure(store, fileDeletion);
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var inbox = account.IMAPFolders.get_ItemByDBID(10);
        inbox.Delete();

        Assert.AreEqual(1, store.DeleteCount);
        Assert.AreEqual(10, store.LastDeletedFolder!.Id);
        Assert.AreEqual(2, account.IMAPFolders.Count);
        Assert.AreEqual(10, account.IMAPFolders[0].ID);
        Assert.AreEqual(20, account.IMAPFolders[1].ID);
        Assert.AreEqual(0, inbox.SubFolders.Count);
        Assert.AreEqual(1, fileDeletion.CallCount);
        Assert.AreEqual(1, fileDeletion.LastResult!.DeletedMessages.Count);
    }

    [TestMethod]
    public void AuthorizedImapFolder_DeleteRemovesOnlySelectedOwnerSubtree()
    {
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03"),
                new ImapFolderAdministrationSnapshot(20, 100, -1, "Archive", true, 2, "2026-06-27 04:05:06"),
                new ImapFolderAdministrationSnapshot(21, 100, 20, "Nested", false, 3, "2026-06-27 05:05:06"),
                new ImapFolderAdministrationSnapshot(30, 200, -1, "Other", true, 1, "2026-06-27 06:05:06")
            });
        ImapFolderAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        account.IMAPFolders.DeleteByDBID(20);

        Assert.AreEqual(1, store.DeleteCount);
        Assert.AreEqual(20, store.LastDeletedFolder!.Id);
        Assert.AreEqual(1, account.IMAPFolders.Count);
        Assert.AreEqual(10, account.IMAPFolders[0].ID);
        Assert.AreEqual(0, account.IMAPFolders[0].SubFolders.Count);

        var missing = Assert.ThrowsExactly<COMException>(() => account.IMAPFolders.DeleteByDBID(20));
        Assert.AreEqual(DispEBadIndex, missing.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedImapFolder_DeleteTreatsStaleAsNoOpAndMapsStoreFailureToEFail()
    {
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03"),
                new ImapFolderAdministrationSnapshot(20, 100, -1, "Archive", true, 2, "2026-06-27 04:05:06")
            })
        {
            ReturnDeleteSuccess = false
        };
        ImapFolderAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var archive = account.IMAPFolders.get_ItemByDBID(20);

        archive.Delete();

        Assert.AreEqual(1, store.DeleteCount);
        Assert.AreEqual(2, account.IMAPFolders.Count);

        store.ThrowDelete = true;
        var error = Assert.ThrowsExactly<COMException>(() => archive.Delete());

        Assert.AreEqual(unchecked((int)0x80004005), error.ErrorCode);
        Assert.AreEqual(2, account.IMAPFolders.Count);
    }

    [TestMethod]
    public void RetainedImapFolder_DeleteRechecksLiveAuthentication()
    {
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03")
            });
        ImapFolderAdministrationRuntimeHost.Configure(store);
        var authenticated = true;
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2),
            () => authenticated);
        var folders = account.IMAPFolders;
        var folder = folders[0];

        authenticated = false;
        var itemError = Assert.ThrowsExactly<COMException>(() => folder.Delete());
        var collectionError = Assert.ThrowsExactly<COMException>(() => folders.DeleteByDBID(10));

        Assert.AreEqual(EAccessDenied, itemError.ErrorCode);
        Assert.AreEqual(EAccessDenied, collectionError.ErrorCode);
        Assert.AreEqual(0, store.DeleteCount);

        authenticated = true;
        folder.Delete();

        Assert.AreEqual(1, store.DeleteCount);
    }

    [TestMethod]
    public void AccountImapFolders_ReusesOneAccountSnapshotAcrossFreshRootAndChildFacades()
    {
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(20, 100, -1, "Archive", true, 2, "2026-06-27 04:05:06"),
                new ImapFolderAdministrationSnapshot(12, 100, 10, "Later", false, 9, "2026-06-27 03:04:05"),
                new ImapFolderAdministrationSnapshot(30, 200, -1, "OtherAccount", true, 1, "2026-06-27 05:06:07"),
                new ImapFolderAdministrationSnapshot(10, 100, -1, "Inbox", true, 42, "2026-06-27 01:02:03"),
                new ImapFolderAdministrationSnapshot(13, 100, 11, "OtherParent", true, 4, "2026-06-27 03:05:05"),
                new ImapFolderAdministrationSnapshot(11, 100, 10, "Child", true, 8, "2026-06-27 02:03:04")
            });
        ImapFolderAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var firstRoots = account.IMAPFolders;
        var secondRoots = account.IMAPFolders;
        Assert.AreNotSame(firstRoots, secondRoots);
        Assert.AreEqual(2, firstRoots.Count);
        Assert.AreEqual(2, secondRoots.Count);
        Assert.AreEqual(1, store.FolderReadCount);
        Assert.AreEqual(10, firstRoots[0].ID);
        Assert.AreEqual(20, firstRoots[1].ID);

        var firstChildren = firstRoots[0].SubFolders;
        var secondChildren = secondRoots[0].SubFolders;

        Assert.AreNotSame(firstChildren, secondChildren);
        Assert.AreEqual(2, firstChildren.Count);
        Assert.AreEqual(2, secondChildren.Count);
        Assert.AreEqual(1, store.FolderReadCount);
        Assert.AreEqual(11, firstChildren[0].ID);
        Assert.AreEqual(12, firstChildren[1].ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = firstChildren.get_ItemByDBID(30)).ErrorCode);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = firstChildren.get_ItemByDBID(13)).ErrorCode);
    }

    [TestMethod]
    public void PublicFolderPermissions_UsesConfiguredRuntimeForSelectedFolder()
    {
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(50, 0, -1, "Public", true, 5, "2026-06-27 00:02:03")
            },
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(
                    500,
                    50,
                    (int)ComAclPermissionType.Anyone,
                    0,
                    0,
                    (int)(ComAclPermission.Lookup | ComAclPermission.Read)),
                new ImapFolderPermissionAdministrationSnapshot(
                    600,
                    60,
                    (int)ComAclPermissionType.User,
                    0,
                    100,
                    (int)ComAclPermission.Lookup)
            });
        ImapFolderAdministrationRuntimeHost.Configure(
            store);
        var folders = IMAPFolders.CreateAuthorized(
            new[]
            {
                new ImapFolderAdministrationSnapshot(50, 0, -1, "Public", true, 5, "2026-06-27 00:02:03")
            });

        var permissions = folders[0].Permissions;

        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(500, permissions[0].ID);
        Assert.AreEqual(50, permissions[0].ShareFolderID);
        Assert.AreEqual(ComAclPermissionType.Anyone, permissions[0].PermissionType);
        Assert.IsTrue(permissions[0].get_Permission(ComAclPermission.Read));
        Assert.AreEqual(1, store.PermissionReadCount);

        store.ReplacePermissions(
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(
                    700,
                    50,
                    (int)ComAclPermissionType.Anyone,
                    0,
                    0,
                    (int)ComAclPermission.Lookup),
                new ImapFolderPermissionAdministrationSnapshot(
                    800,
                    60,
                    (int)ComAclPermissionType.User,
                    0,
                    100,
                    (int)ComAclPermission.Lookup)
            });

        permissions.Refresh();

        Assert.AreEqual(2, store.PermissionReadCount);
        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(700, permissions[0].ID);
        Assert.AreEqual(50, permissions[0].ShareFolderID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = permissions.get_ItemByDBID(500)).ErrorCode);
    }

    [TestMethod]
    public void RetainedPermissionChildren_RecheckLiveAuthentication()
    {
        var isAuthenticated = true;
        AccountAdministrationRuntimeHost.Configure(
            new FixedAccountAdministrationStore(
                new[]
                {
                    new AccountAdministrationSnapshot(100, 1, "account@example.test", true, 0)
                }));
        GroupAdministrationRuntimeHost.Configure(
            new FixedGroupAdministrationStore(
                new[] { new GroupAdministrationSnapshot(200, "Support") }));
        var store = new FixedImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(50, 0, -1, "Public", true, 5, "2026-06-27 00:02:03")
            },
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(
                    500,
                    50,
                    (int)ComAclPermissionType.User,
                    0,
                    100,
                    (int)ComAclPermission.Lookup),
                new ImapFolderPermissionAdministrationSnapshot(
                    600,
                    50,
                    (int)ComAclPermissionType.Group,
                    200,
                    0,
                    (int)ComAclPermission.Read)
            });
        ImapFolderAdministrationRuntimeHost.Configure(store);
        var state = ImapFolderAdministrationRuntimeHost.CreateAuthorizedState(0);
        var folder = IMAPFolder.CreateAuthorized(
            new ImapFolderAdministrationSnapshot(50, 0, -1, "Public", true, 5, "2026-06-27 00:02:03"),
            state,
            () => isAuthenticated);

        var permissions = folder.Permissions;
        var retainedAccount = permissions[0].Account;
        var retainedGroup = permissions[1].Group;

        Assert.AreEqual(100, retainedAccount.ID);
        Assert.AreEqual(200, retainedGroup.ID);

        isAuthenticated = false;

        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => _ = retainedAccount.ID).ErrorCode);
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => retainedGroup.Name = "Changed").ErrorCode);
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => _ = permissions[0].Account.ID).ErrorCode);
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => permissions[1].Group.Name = "Changed").ErrorCode);
    }

    [TestMethod]
    public void SettingsPublicFolders_PropagatesAuthenticationToPermissionChildren()
    {
        var isAuthenticated = true;
        AccountAdministrationRuntimeHost.Configure(
            new FixedAccountAdministrationStore(
                new[]
                {
                    new AccountAdministrationSnapshot(100, 1, "account@example.test", true, 0)
                }));
        GroupAdministrationRuntimeHost.Configure(
            new FixedGroupAdministrationStore(
                new[] { new GroupAdministrationSnapshot(200, "Support") }));
        ImapFolderAdministrationRuntimeHost.Configure(
            new FixedImapFolderAdministrationStore(
                new[]
                {
                    new ImapFolderAdministrationSnapshot(50, 0, -1, "Public", true, 5, "2026-06-27 00:02:03")
                },
                new[]
                {
                    new ImapFolderPermissionAdministrationSnapshot(
                        500,
                        50,
                        (int)ComAclPermissionType.User,
                        0,
                        100,
                        (int)ComAclPermission.Lookup),
                    new ImapFolderPermissionAdministrationSnapshot(
                        600,
                        50,
                        (int)ComAclPermissionType.Group,
                        200,
                        0,
                        (int)ComAclPermission.Read)
                }));

        IInterfaceSettings settings = Settings.CreateAuthorized(
            isServerAdministrator: () => isAuthenticated);
        var permissions = settings.PublicFolders[0].Permissions;
        var retainedAccount = permissions[0].Account;
        var retainedGroup = permissions[1].Group;

        Assert.AreEqual(100, retainedAccount.ID);
        Assert.AreEqual(200, retainedGroup.ID);

        isAuthenticated = false;

        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => _ = retainedAccount.ID).ErrorCode);
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => retainedGroup.Name = "Changed").ErrorCode);
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => _ = permissions[0].Account.ID).ErrorCode);
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => permissions[1].Group.Name = "Changed").ErrorCode);
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

    private sealed class FixedAccountAdministrationStore(
        IReadOnlyList<AccountAdministrationSnapshot> accounts) : IAccountAdministrationStore
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

    private sealed class FixedGroupAdministrationStore(
        IReadOnlyList<GroupAdministrationSnapshot> groups) : IGroupAdministrationStore
    {
        public ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> GetGroupsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(groups);
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
        IReadOnlyList<ImapFolderAdministrationSnapshot> folders,
        IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>? permissions = null) : IImapFolderAdministrationStore, IImapFolderAdministrationMutationStore, IImapFolderAdministrationDeletionStore
    {
        private IReadOnlyList<ImapFolderAdministrationSnapshot> _folders = folders;
        private IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> _permissions =
            permissions ?? Array.Empty<ImapFolderPermissionAdministrationSnapshot>();

        public int PermissionReadCount { get; private set; }
        public int FolderReadCount { get; private set; }
        public int InsertCount { get; private set; }
        public int LastInsertAccountId { get; private set; }
        public int LastInsertParentId { get; private set; }
        public string? LastInsertName { get; private set; }
        public bool ReturnMisScopedInsert { get; set; }
        public bool ReturnUpdateSuccess { get; set; } = true;
        public int UpdateCount { get; private set; }
        public ImapFolderAdministrationSnapshot? LastUpdatedFolder { get; private set; }
        public bool ReturnDeleteSuccess { get; set; } = true;
        public bool ThrowDelete { get; set; }
        public int DeleteCount { get; private set; }
        public ImapFolderAdministrationSnapshot? LastDeletedFolder { get; private set; }
        public IReadOnlyList<ImapFolderAdministrationDeletedMessage> DeletedMessages { get; set; } =
            Array.Empty<ImapFolderAdministrationDeletedMessage>();

        public void ReplacePermissions(IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> permissions)
        {
            _permissions = permissions;
        }

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetFoldersForAccountAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            FolderReadCount++;
            return ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                _folders.Where(folder => folder.AccountId == accountId)
                    .OrderBy(folder => folder.Id)
                    .ToArray());
        }

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                _folders.Where(folder => folder.AccountId == accountId && folder.ParentId == -1)
                    .OrderBy(folder => folder.Id)
                    .ToArray());

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetChildFoldersAsync(
            int parentFolderId,
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                _folders.Where(folder => folder.AccountId == accountId && folder.ParentId == parentFolderId)
                    .OrderBy(folder => folder.Id)
                    .ToArray());

        public ValueTask<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>> GetFolderPermissionsAsync(
            int folderId,
            CancellationToken cancellationToken)
        {
            PermissionReadCount++;
            return ValueTask.FromResult<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>>(
                _permissions
                    .Where(permission => permission.ShareFolderId == folderId)
                    .OrderBy(permission => permission.Id)
                    .ToArray());
        }

        public ValueTask<ImapFolderAdministrationSnapshot> InsertFolderAsync(
            int accountId,
            int parentFolderId,
            string encodedName,
            bool subscribed,
            CancellationToken cancellationToken)
        {
            InsertCount++;
            LastInsertAccountId = accountId;
            LastInsertParentId = parentFolderId;
            LastInsertName = encodedName;
            var snapshot = new ImapFolderAdministrationSnapshot(
                101,
                ReturnMisScopedInsert ? accountId + 1 : accountId,
                parentFolderId,
                encodedName,
                subscribed,
                0,
                "2026-08-01 00:00:00");
            _folders = _folders.Append(snapshot).ToArray();
            return ValueTask.FromResult(snapshot);
        }

        public ValueTask<bool> UpdateFolderAsync(
            ImapFolderAdministrationSnapshot folder,
            CancellationToken cancellationToken)
        {
            UpdateCount++;
            LastUpdatedFolder = folder;
            if (!ReturnUpdateSuccess)
            {
                return ValueTask.FromResult(false);
            }

            _folders = _folders
                .Select(existing => existing.Id == folder.Id ? folder : existing)
                .ToArray();
            return ValueTask.FromResult(true);
        }

        public ValueTask<ImapFolderAdministrationDeletionResult> DeleteFolderAsync(
            ImapFolderAdministrationSnapshot folder,
            CancellationToken cancellationToken)
        {
            DeleteCount++;
            LastDeletedFolder = folder;
            if (ThrowDelete)
            {
                throw new InvalidOperationException("delete failure");
            }

            return ValueTask.FromResult(
                new ImapFolderAdministrationDeletionResult(ReturnDeleteSuccess, DeletedMessages));
        }
    }

    private sealed class RecordingMessageFileDeletionRuntime : IImapFolderMessageFileDeletionRuntime
    {
        public int CallCount { get; private set; }
        public ImapFolderAdministrationDeletionResult? LastResult { get; private set; }
        public bool ReturnSuccess { get; set; } = true;

        public bool TryDeleteAll(ImapFolderAdministrationDeletionResult result)
        {
            CallCount++;
            LastResult = result;
            return ReturnSuccess;
        }
    }

    private sealed class EmptyMessageAdministrationStore : IMessageAdministrationStore
    {
        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(Array.Empty<MessageAdministrationSnapshot>());

        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
            int accountId,
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(Array.Empty<MessageAdministrationSnapshot>());
    }
}
