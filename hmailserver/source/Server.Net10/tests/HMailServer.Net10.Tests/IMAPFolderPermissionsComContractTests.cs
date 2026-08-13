using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class IMAPFolderPermissionsComContractTests
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsMarshalingAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceIMAPFolderPermission),
            "A951C988-0D2C-42CA-A9D3-FE7A78F1AB25",
            new[]
            {
                "get_ID", "get_ShareFolderID", "get_PermissionType", "set_PermissionType",
                "get_PermissionGroupID", "set_PermissionGroupID", "get_PermissionAccountID",
                "set_PermissionAccountID", "get_Value", "set_Value", "get_Permission",
                "set_Permission", "Save", "Delete", "get_Account", "get_Group"
            });
        Assert.AreEqual(
            8,
            typeof(IInterfaceIMAPFolderPermission).GetMethod("get_Permission")
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            typeof(IInterfaceIMAPFolderPermission).GetMethod("get_Permission")
                ?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            typeof(IInterfaceIMAPFolderPermission).GetMethod("set_Permission")
                ?.GetParameters()[1].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(
            12,
            typeof(IInterfaceIMAPFolderPermission).GetProperty(nameof(IInterfaceIMAPFolderPermission.Group))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceIMAPFolderPermissions),
            "CBE3FE9E-3642-4BA1-9BE0-6E766C0DE961",
            new[]
            {
                "get_Item", "get_Count", "Delete", "Refresh", "Add", "get_ItemByDBID",
                "DeleteByDBID", "get_ItemByName"
            });
        Assert.AreEqual(
            7,
            typeof(IInterfaceIMAPFolderPermissions).GetMethod("get_ItemByName")
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void Enums_PreserveLegacyGuidsAndValues()
    {
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD10"), typeof(ComAclPermission).GUID);
        CollectionAssert.AreEqual(
            new[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 },
            Enum.GetValues<ComAclPermission>().Select(static value => (int)value).ToArray());

        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD11"), typeof(ComAclPermissionType).GUID);
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2 },
            Enum.GetValues<ComAclPermissionType>().Select(static value => (int)value).ToArray());
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<IMAPFolderPermissions>(
            "A6B391A4-72C8-44AA-9480-9FB3BD593B46",
            "hMailServer.IMAPFolderPermissions.1",
            typeof(IInterfaceIMAPFolderPermissions));
        AssertComClass<IMAPFolderPermission>(
            "D5800098-1033-4D83-9E06-94F6E1B557F9",
            "hMailServer.IMAPFolderPermission.1",
            typeof(IInterfaceIMAPFolderPermission));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var permissionsError = Assert.ThrowsExactly<COMException>(() => _ = new IMAPFolderPermissions().Count);
        var permissionsRefreshError = Assert.ThrowsExactly<COMException>(new IMAPFolderPermissions().Refresh);
        var permissionsDeleteError = Assert.ThrowsExactly<COMException>(() => new IMAPFolderPermissions().Delete(0));
        var permissionsDeleteByDBIDError = Assert.ThrowsExactly<COMException>(() => new IMAPFolderPermissions().DeleteByDBID(10));
        var permissionsAddError = Assert.ThrowsExactly<COMException>(new IMAPFolderPermissions().Add);
        var permissionError = Assert.ThrowsExactly<COMException>(() => _ = new IMAPFolderPermission().ID);
        var permissionDeleteError = Assert.ThrowsExactly<COMException>(new IMAPFolderPermission().Delete);

        Assert.AreEqual(EAccessDenied, permissionsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, permissionsRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, permissionsDeleteByDBIDError.ErrorCode);
        Assert.AreEqual(EAccessDenied, permissionsDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, permissionsAddError.ErrorCode);
        Assert.AreEqual(EAccessDenied, permissionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, permissionDeleteError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByIndexDeletesSelectedPermissionAndUpdatesSnapshot()
    {
        var calls = new List<(int FolderId, int PermissionId)>();
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1),
                new ImapFolderPermissionAdministrationSnapshot(20, 50, 2, 0, 0, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            (folderId, permissionId) =>
            {
                calls.Add((folderId, permissionId));
                return ValueTask.FromResult(true);
            });

        permissions.Delete(1);

        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(10, permissions[0].ID);
        CollectionAssert.AreEqual(new[] { (50, 20) }, calls);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByIndexTreatsNegativeAndOutOfRangeAsNoOp()
    {
        var calls = new List<(int FolderId, int PermissionId)>();
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1),
                new ImapFolderPermissionAdministrationSnapshot(20, 50, 2, 0, 0, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            (folderId, permissionId) =>
            {
                calls.Add((folderId, permissionId));
                return ValueTask.FromResult(true);
            });

        permissions.Delete(-1);
        permissions.Delete(2);

        Assert.AreEqual(0, calls.Count);
        Assert.AreEqual(2, permissions.Count);
        Assert.AreEqual(10, permissions[0].ID);
        Assert.AreEqual(20, permissions[1].ID);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByIndexRetainsSnapshotWhenCallbackReturnsFalse()
    {
        var calls = new List<(int FolderId, int PermissionId)>();
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            (folderId, permissionId) =>
            {
                calls.Add((folderId, permissionId));
                return ValueTask.FromResult(false);
            });

        permissions.Delete(0);

        Assert.AreEqual(1, calls.Count);
        CollectionAssert.AreEqual(new[] { (50, 10) }, calls);
        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(10, permissions[0].ID);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByIndexMapsCallbackFailureToEFailAndRetainsSnapshot()
    {
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            static (_, _) => ValueTask.FromException<bool>(new InvalidOperationException("Simulated store failure.")));

        var error = Assert.ThrowsExactly<COMException>(() => permissions.Delete(0));

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(10, permissions[0].ID);
    }

    [TestMethod]
    public void AuthorizedItem_DeleteUsesOwningFolderAndPermissionIdsAndUpdatesSnapshot()
    {
        var calls = new List<(int FolderId, int PermissionId)>();
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1),
                new ImapFolderPermissionAdministrationSnapshot(20, 50, 2, 0, 0, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            (folderId, permissionId) =>
            {
                calls.Add((folderId, permissionId));
                return ValueTask.FromResult(true);
            });

        var selected = permissions[1];
        selected.Delete();
        selected.Delete();

        Assert.AreEqual(1, calls.Count);
        CollectionAssert.AreEqual(new[] { (50, 20) }, calls);
        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(10, permissions[0].ID);
    }

    [TestMethod]
    public void AuthorizedItem_DeleteByDBIDWrapperBecomesNoOpAfterItsSiblingDeletes()
    {
        var calls = new List<(int FolderId, int PermissionId)>();
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            (folderId, permissionId) =>
            {
                calls.Add((folderId, permissionId));
                return ValueTask.FromResult(true);
            });

        var first = permissions[0];
        var second = permissions.get_ItemByDBID(10);
        first.Delete();
        second.Delete();

        Assert.AreEqual(1, calls.Count);
        CollectionAssert.AreEqual(new[] { (50, 10) }, calls);
        Assert.AreEqual(0, permissions.Count);
    }

    [TestMethod]
    public void AuthorizedItem_DeleteRetainsSnapshotWhenCallbackReturnsFalse()
    {
        var calls = new List<(int FolderId, int PermissionId)>();
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            (folderId, permissionId) =>
            {
                calls.Add((folderId, permissionId));
                return ValueTask.FromResult(false);
            });

        permissions[0].Delete();

        Assert.AreEqual(1, calls.Count);
        CollectionAssert.AreEqual(new[] { (50, 10) }, calls);
        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(10, permissions[0].ID);
    }

    [TestMethod]
    public void AuthorizedItem_DeleteMapsCallbackFailureToEFailAndRetainsSnapshot()
    {
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            static (_, _) => ValueTask.FromException<bool>(new InvalidOperationException("Simulated store failure.")));

        var error = Assert.ThrowsExactly<COMException>(() => permissions[0].Delete());

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(10, permissions[0].ID);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByDBIDDeletesSelectedPermissionAndUpdatesSnapshot()
    {
        var calls = new List<(int FolderId, int PermissionId)>();
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1),
                new ImapFolderPermissionAdministrationSnapshot(20, 50, 2, 0, 0, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            (folderId, permissionId) =>
            {
                calls.Add((folderId, permissionId));
                return ValueTask.FromResult(true);
            });

        permissions.DeleteByDBID(10);

        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(20, permissions[0].ID);
        CollectionAssert.AreEqual(new[] { (50, 10) }, calls);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByDBIDTreatsUnknownForeignAndRepeatedIdsAsNoOp()
    {
        var calls = new List<(int FolderId, int PermissionId)>();
        var deleteResult = false;
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            (folderId, permissionId) =>
            {
                calls.Add((folderId, permissionId));
                return ValueTask.FromResult(deleteResult);
            });

        permissions.DeleteByDBID(999);
        Assert.AreEqual(0, calls.Count);
        Assert.AreEqual(1, permissions.Count);

        permissions.DeleteByDBID(10);
        Assert.AreEqual(1, calls.Count);
        Assert.AreEqual(1, permissions.Count);

        deleteResult = true;
        permissions.DeleteByDBID(10);
        permissions.DeleteByDBID(10);

        Assert.AreEqual(2, calls.Count);
        Assert.AreEqual(0, permissions.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByDBIDMapsStoreFailureToEFailAndRetainsSnapshot()
    {
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 1, 0, 100, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            static (_, _) => ValueTask.FromException<bool>(new InvalidOperationException("Simulated store failure.")));

        var error = Assert.ThrowsExactly<COMException>(() => permissions.DeleteByDBID(10));

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(10, permissions[0].ID);
    }

    [TestMethod]
    public void AuthorizedPermissionMutations_HoldAuthorizationLeaseAcrossStoreCallbacks()
    {
        var activeLeases = 0;
        var disposedLeases = 0;
        var callbackCount = 0;
        var observedLeaseCounts = new List<int>();
        var snapshots = new[]
        {
            new ImapFolderPermissionAdministrationSnapshot(10, 50, 0, 0, 100, 1),
            new ImapFolderPermissionAdministrationSnapshot(20, 50, 1, 200, 0, 1),
            new ImapFolderPermissionAdministrationSnapshot(30, 50, 0, 0, 200, 1)
        };

        void Probe()
        {
            observedLeaseCounts.Add(activeLeases);
            callbackCount++;
        }

        Func<CancellationToken, ValueTask<IDisposable?>> leaseFactory = _ =>
        {
            activeLeases++;
            return ValueTask.FromResult<IDisposable?>(
                new TrackingLease(() =>
                {
                    activeLeases--;
                    disposedLeases++;
                }));
        };

        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            snapshots,
            () => snapshots,
            delete: (_, _) =>
            {
                Probe();
                return ValueTask.FromResult(true);
            },
            insert: (type, groupId, accountId, value) =>
            {
                Probe();
                return ValueTask.FromResult<ImapFolderPermissionAdministrationSnapshot?>(
                    new ImapFolderPermissionAdministrationSnapshot(77, 50, type, groupId, accountId, value));
            },
            update: (_, _, _, _, _) =>
            {
                Probe();
                return ValueTask.FromResult(true);
            },
            authorizationLeaseFactory: leaseFactory);

        var added = permissions.Add();
        added.PermissionAccountID = 300;
        added.Save();
        permissions[0].Save();
        permissions[1].Delete();
        permissions.DeleteByDBID(30);
        permissions.Delete(0);

        Assert.AreEqual(5, callbackCount);
        CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1 }, observedLeaseCounts);
        Assert.AreEqual(0, activeLeases);
        Assert.AreEqual(5, disposedLeases);
    }

    [TestMethod]
    public void AuthorizedPermissionMutations_DenyBeforeStoreWhenAuthorizationLeaseIsUnavailable()
    {
        var leaseRequests = 0;
        var callbackCount = 0;
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 0, 0, 100, 1),
                new ImapFolderPermissionAdministrationSnapshot(20, 50, 1, 200, 0, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            delete: (_, _) =>
            {
                callbackCount++;
                return ValueTask.FromResult(true);
            },
            insert: (_, _, _, _) =>
            {
                callbackCount++;
                return ValueTask.FromResult<ImapFolderPermissionAdministrationSnapshot?>(null);
            },
            update: (_, _, _, _, _) =>
            {
                callbackCount++;
                return ValueTask.FromResult(true);
            },
            authorizationLeaseFactory: _ =>
            {
                leaseRequests++;
                return ValueTask.FromResult<IDisposable?>(null);
            });

        var added = permissions.Add();
        added.PermissionAccountID = 300;
        var newSaveError = Assert.ThrowsExactly<COMException>(added.Save);
        var existingSaveError = Assert.ThrowsExactly<COMException>(permissions[0].Save);
        var itemDeleteError = Assert.ThrowsExactly<COMException>(permissions[0].Delete);
        var collectionDeleteError = Assert.ThrowsExactly<COMException>(() => permissions.Delete(0));
        var databaseDeleteError = Assert.ThrowsExactly<COMException>(() => permissions.DeleteByDBID(10));

        Assert.AreEqual(EAccessDenied, newSaveError.ErrorCode);
        Assert.AreEqual(EAccessDenied, existingSaveError.ErrorCode);
        Assert.AreEqual(EAccessDenied, itemDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, collectionDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, databaseDeleteError.ErrorCode);
        Assert.AreEqual(5, leaseRequests);
        Assert.AreEqual(0, callbackCount);
        Assert.AreEqual(2, permissions.Count);
    }

    [TestMethod]
    public void AuthorizedPermissionMutations_DenyAfterLogoutWithoutLeaseFactory()
    {
        var authenticated = true;
        var callbackCount = 0;
        var snapshot = new ImapFolderPermissionAdministrationSnapshot(10, 50, 0, 0, 100, 1);
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[] { snapshot },
            () => new[] { snapshot },
            delete: (_, _) =>
            {
                callbackCount++;
                return ValueTask.FromResult(true);
            },
            insert: (_, _, _, _) =>
            {
                callbackCount++;
                return ValueTask.FromResult<ImapFolderPermissionAdministrationSnapshot?>(null);
            },
            update: (_, _, _, _, _) =>
            {
                callbackCount++;
                return ValueTask.FromResult(true);
            },
            isAuthenticated: () => authenticated);

        var retained = permissions[0];
        retained.Value = 2;
        authenticated = false;

        var errors = new[]
        {
            Assert.ThrowsExactly<COMException>(permissions.Add),
            Assert.ThrowsExactly<COMException>(retained.Save),
            Assert.ThrowsExactly<COMException>(retained.Delete),
            Assert.ThrowsExactly<COMException>(() => permissions.Delete(0)),
            Assert.ThrowsExactly<COMException>(() => permissions.DeleteByDBID(10))
        };

        Assert.IsTrue(errors.All(error => error.ErrorCode == EAccessDenied));
        Assert.AreEqual(0, callbackCount);
        Assert.AreEqual(1, permissions.Count);
    }

    [TestMethod]
    public void RetainedPermission_AllowsLegacyReadAndStageAfterLogoutButSaveRemainsDenied()
    {
        var authenticated = true;
        var snapshot = new ImapFolderPermissionAdministrationSnapshot(10, 50, 0, 0, 100, 1);
        var updateCalls = 0;
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[] { snapshot },
            () => new[] { snapshot },
            delete: null,
            update: (_, _, _, _, _) =>
            {
                updateCalls++;
                return ValueTask.FromResult(true);
            },
            isAuthenticated: () => authenticated);

        var retained = permissions[0];
        authenticated = false;

        Assert.AreEqual(10, retained.ID);
        Assert.AreEqual(50, retained.ShareFolderID);
        Assert.AreEqual(ComAclPermissionType.User, retained.PermissionType);
        Assert.IsTrue(retained.get_Permission(ComAclPermission.Lookup));

        retained.PermissionType = ComAclPermissionType.User;
        retained.PermissionAccountID = 200;
        retained.set_Permission(ComAclPermission.Read, true);

        Assert.AreEqual(200, retained.PermissionAccountID);
        Assert.IsTrue(retained.get_Permission(ComAclPermission.Read));
        var saveError = Assert.ThrowsExactly<COMException>(retained.Save);

        Assert.AreEqual(EAccessDenied, saveError.ErrorCode);
        Assert.AreEqual(0, updateCalls);
        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(100, permissions[0].PermissionAccountID);
    }

    [TestMethod]
    public void AuthorizedCollection_AddStagesNewPermissionAndAppendsOnlyAfterValidatedSave()
    {
        var calls = new List<(int Type, int GroupId, int AccountId, int Value)>();
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            delete: null,
            insert: (type, groupId, accountId, value) =>
            {
                calls.Add((type, groupId, accountId, value));
                return ValueTask.FromResult<ImapFolderPermissionAdministrationSnapshot?>(
                    new ImapFolderPermissionAdministrationSnapshot(
                        77,
                        50,
                        type,
                        groupId,
                        accountId,
                        value));
            });

        var added = permissions.Add();

        AssertPermission(added, 0, 50, ComAclPermissionType.User, 0, 0, 0);
        added.PermissionType = ComAclPermissionType.Group;
        added.PermissionGroupID = 200;
        added.Value = 1;
        added.set_Permission(ComAclPermission.Read, true);
        added.set_Permission(ComAclPermission.Lookup, false);

        AssertPermission(added, 0, 50, ComAclPermissionType.Group, 200, 0, 2);
        Assert.AreEqual(0, permissions.Count);

        added.Save();

        CollectionAssert.AreEqual(new[] { (1, 200, 0, 2) }, calls);
        AssertPermission(added, 77, 50, ComAclPermissionType.Group, 200, 0, 2);
        Assert.AreEqual(1, permissions.Count);
        AssertPermission(permissions[0], 77, 50, ComAclPermissionType.Group, 200, 0, 2);
    }

    [TestMethod]
    public void AuthorizedCollection_AddRejectsInvalidHolderBeforeStoreAndRetainsStagedItem()
    {
        var insertCount = 0;
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            delete: null,
            insert: (_, _, _, _) =>
            {
                insertCount++;
                return ValueTask.FromResult<ImapFolderPermissionAdministrationSnapshot?>(null);
            });

        var added = permissions.Add();
        added.PermissionType = ComAclPermissionType.Anyone;
        added.PermissionAccountID = 100;

        var error = Assert.ThrowsExactly<COMException>(added.Save);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(0, insertCount);
        AssertPermission(added, 0, 50, ComAclPermissionType.Anyone, 0, 100, 0);
        Assert.AreEqual(0, permissions.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_AddMapsFalseOrExceptionToEFailAndRetainsStagedItem()
    {
        foreach (var insert in new Func<int, int, int, int, ValueTask<ImapFolderPermissionAdministrationSnapshot?>>[]
                 {
                     static (_, _, _, _) => ValueTask.FromResult<ImapFolderPermissionAdministrationSnapshot?>(null),
                     static (_, _, _, _) => ValueTask.FromException<ImapFolderPermissionAdministrationSnapshot?>(
                         new InvalidOperationException("Simulated store failure."))
                 })
        {
            IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
                50,
                Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
                () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
                delete: null,
                insert: insert);
            var added = permissions.Add();
            added.PermissionType = ComAclPermissionType.User;
            added.PermissionAccountID = 100;
            added.Value = 4;

            var error = Assert.ThrowsExactly<COMException>(added.Save);

            Assert.AreEqual(EFail, error.ErrorCode);
            AssertPermission(added, 0, 50, ComAclPermissionType.User, 0, 100, 4);
            Assert.AreEqual(0, permissions.Count);
        }
    }

    [TestMethod]
    public void AuthorizedExistingItem_SaveUpdatesAllFieldsAndPermissionBitsThroughEachLookup()
    {
        var updates = new List<(int Id, int Type, int GroupId, int AccountId, int Value)>();
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(10, 50, 0, 0, 100, 1),
                new ImapFolderPermissionAdministrationSnapshot(20, 50, 2, 0, 0, 1),
                new ImapFolderPermissionAdministrationSnapshot(30, 50, 1, 200, 0, 1)
            },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            delete: null,
            insert: null,
            update: (permission, type, groupId, accountId, value) =>
            {
                updates.Add((permission.Id, type, groupId, accountId, value));
                return ValueTask.FromResult(true);
            });

        var byIndex = permissions[0];
        byIndex.PermissionType = ComAclPermissionType.Group;
        byIndex.PermissionGroupID = 300;
        byIndex.PermissionAccountID = 0;
        byIndex.Value = 0;
        byIndex.set_Permission(ComAclPermission.Read, true);
        byIndex.set_Permission(ComAclPermission.Administer, true);
        byIndex.Save();

        var byDbid = permissions.get_ItemByDBID(20);
        byDbid.PermissionType = ComAclPermissionType.User;
        byDbid.PermissionGroupID = 0;
        byDbid.PermissionAccountID = 400;
        byDbid.Value = 0;
        byDbid.set_Permission(ComAclPermission.WriteSeen, true);
        byDbid.Save();

        var byName = permissions.get_ItemByName("aclpermission-30");
        byName.PermissionType = ComAclPermissionType.Anyone;
        byName.PermissionGroupID = 0;
        byName.PermissionAccountID = 0;
        byName.Value = 0;
        byName.set_Permission(ComAclPermission.Expunge, true);
        byName.Save();

        CollectionAssert.AreEqual(
            new[]
            {
                (10, 1, 300, 0, 1026),
                (20, 0, 0, 400, 4),
                (30, 2, 0, 0, 512)
            },
            updates);
        AssertPermission(byIndex, 10, 50, ComAclPermissionType.Group, 300, 0, 1026);
        AssertPermission(byDbid, 20, 50, ComAclPermissionType.User, 0, 400, 4);
        AssertPermission(byName, 30, 50, ComAclPermissionType.Anyone, 0, 0, 512);
        AssertPermission(permissions[0], 10, 50, ComAclPermissionType.Group, 300, 0, 1026);
        AssertPermission(permissions.get_ItemByDBID(20), 20, 50, ComAclPermissionType.User, 0, 400, 4);
        AssertPermission(permissions.get_ItemByName("aclpermission-30"), 30, 50, ComAclPermissionType.Anyone, 0, 0, 512);
    }

    [TestMethod]
    public void AuthorizedExistingItem_SaveRejectsInvalidHolderAndFlagBeforeStore()
    {
        var calls = 0;
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[] { new ImapFolderPermissionAdministrationSnapshot(10, 50, 0, 0, 100, 1) },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            delete: null,
            update: (_, _, _, _, _) =>
            {
                calls++;
                return ValueTask.FromResult(true);
            });
        var item = permissions[0];
        item.PermissionType = ComAclPermissionType.Anyone;
        item.PermissionAccountID = 100;

        var holderError = Assert.ThrowsExactly<COMException>(item.Save);
        Assert.AreEqual(EFail, holderError.ErrorCode);
        Assert.AreEqual(0, calls);
        AssertPermission(item, 10, 50, ComAclPermissionType.Anyone, 0, 100, 1);
        AssertPermission(permissions[0], 10, 50, ComAclPermissionType.User, 0, 100, 1);

        var flagError = Assert.ThrowsExactly<COMException>(
            () => item.set_Permission((ComAclPermission)2048, true));
        Assert.AreEqual(EFail, flagError.ErrorCode);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void AuthorizedExistingItem_SaveMapsFalseAndExceptionToEFailAndRetainsState()
    {
        foreach (var update in new Func<ImapFolderPermissionAdministrationSnapshot, int, int, int, int, ValueTask<bool>>[]
                 {
                     static (_, _, _, _, _) => ValueTask.FromResult(false),
                     static (_, _, _, _, _) => ValueTask.FromException<bool>(new InvalidOperationException("Simulated store failure."))
                 })
        {
            IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
                50,
                new[] { new ImapFolderPermissionAdministrationSnapshot(10, 50, 0, 0, 100, 1) },
                () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
                delete: null,
                update: update);
            var item = permissions[0];
            item.PermissionType = ComAclPermissionType.Group;
            item.PermissionGroupID = 200;
            item.PermissionAccountID = 0;
            item.Value = 2;

            var error = Assert.ThrowsExactly<COMException>(item.Save);

            Assert.AreEqual(EFail, error.ErrorCode);
            AssertPermission(item, 10, 50, ComAclPermissionType.Group, 200, 0, 2);
            AssertPermission(permissions[0], 10, 50, ComAclPermissionType.User, 0, 100, 1);
        }
    }

    [TestMethod]
    public void ExistingItem_SaveDeniesPrivateForeignAndStaleOrUnownedWrappers()
    {
        var snapshot = new ImapFolderPermissionAdministrationSnapshot(10, 50, 0, 0, 100, 1);
        var privateItem = IMAPFolderPermissions.CreateAuthorized(new[] { snapshot })[0];
        Assert.AreEqual(ENotImplemented, Assert.ThrowsExactly<COMException>(
            () => privateItem.Value = 2).ErrorCode);

        IInterfaceIMAPFolderPermissions foreign = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[] { snapshot with { ShareFolderId = 51 } },
            () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
            delete: null,
            update: static (_, _, _, _, _) => ValueTask.FromResult(true));
        Assert.AreEqual(ENotImplemented, Assert.ThrowsExactly<COMException>(
            () => foreign[0].Value = 2).ErrorCode);

        var refreshed = false;
        IInterfaceIMAPFolderPermissions staleCollection = IMAPFolderPermissions.CreateAuthorized(
            50,
            new[] { snapshot },
            () =>
            {
                refreshed = true;
                return new[] { snapshot with { Value = 2 } };
            },
            delete: null,
            update: static (_, _, _, _, _) => ValueTask.FromResult(true));
        var stale = staleCollection[0];
        staleCollection.Refresh();
        Assert.IsTrue(refreshed);
        stale.Value = 4;
        Assert.AreEqual(EFail, Assert.ThrowsExactly<COMException>(stale.Save).ErrorCode);
        Assert.AreEqual(2, staleCollection[0].Value);
    }

    [TestMethod]
    public void AuthorizedCollection_AddRejectsMalformedReturnedOwnerAndIdentity()
    {
        foreach (var returned in new[]
                 {
                     new ImapFolderPermissionAdministrationSnapshot(0, 50, 0, 0, 100, 1),
                     new ImapFolderPermissionAdministrationSnapshot(77, 51, 0, 0, 100, 1),
                     new ImapFolderPermissionAdministrationSnapshot(77, 50, 1, 0, 100, 1)
                 })
        {
            IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
                50,
                Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
                () => Array.Empty<ImapFolderPermissionAdministrationSnapshot>(),
                delete: null,
                insert: (_, _, _, _) => ValueTask.FromResult<ImapFolderPermissionAdministrationSnapshot?>(returned));
            var added = permissions.Add();
            added.PermissionAccountID = 100;
            added.Value = 1;

            var error = Assert.ThrowsExactly<COMException>(added.Save);

            Assert.AreEqual(EFail, error.ErrorCode);
            Assert.AreEqual(0, added.ID);
            Assert.AreEqual(0, permissions.Count);
        }
    }

    [TestMethod]
    public void AuthorizedSettingsPublicFolderPermissions_AddUsesAuthenticatedOwningFolderScope()
    {
        var store = new PublicFolderPermissionStore();
        ImapFolderAdministrationRuntimeHost.Configure(store);
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var permissions = settings.PublicFolders.get_ItemByDBID(50).Permissions;
        var added = permissions.Add();
        added.PermissionAccountID = 100;
        added.Value = (int)ComAclPermission.Read;
        added.Save();

        Assert.AreEqual(50, store.LastInsertFolderId);
        Assert.AreEqual(100, store.LastInsertAccountId);
        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(501, permissions[0].ID);

        var denied = Assert.ThrowsExactly<COMException>(() => _ = new Settings().PublicFolders);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlyAclPermissionSnapshots()
    {
        AccountAdministrationRuntimeHost.Configure(
            new FixedAccountAdministrationStore(
                new[]
                {
                    new AccountAdministrationSnapshot(100, 50, "acl-user@example.test", true, 0)
                }));
        GroupAdministrationRuntimeHost.Configure(
            new FixedGroupAdministrationStore(
                new[]
                {
                    new GroupAdministrationSnapshot(200, "ACL Group")
                }));

        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(
                    10,
                    50,
                    (int)ComAclPermissionType.User,
                    0,
                    100,
                    (int)(ComAclPermission.Lookup | ComAclPermission.Read | ComAclPermission.Administer)),
                new ImapFolderPermissionAdministrationSnapshot(
                    20,
                    50,
                    (int)ComAclPermissionType.Anyone,
                    0,
                    0,
                    (int)ComAclPermission.Lookup),
                new ImapFolderPermissionAdministrationSnapshot(
                    30,
                    50,
                    (int)ComAclPermissionType.Group,
                    200,
                    0,
                    (int)ComAclPermission.Lookup)
            });

        Assert.AreEqual(3, permissions.Count);
        AssertPermission(
            permissions[0],
            10,
            50,
            ComAclPermissionType.User,
            0,
            100,
            (int)(ComAclPermission.Lookup | ComAclPermission.Read | ComAclPermission.Administer));
        Assert.AreEqual(20, permissions.get_ItemByDBID(20).ID);
        Assert.AreEqual(10, permissions.get_ItemByName("aclpermission-10").ID);
        Assert.IsTrue(permissions[0].get_Permission(ComAclPermission.Lookup));
        Assert.IsTrue(permissions[0].get_Permission(ComAclPermission.Read));
        Assert.IsFalse(permissions[0].get_Permission(ComAclPermission.WriteSeen));
        Assert.AreEqual(100, permissions[0].Account.ID);
        Assert.AreEqual("acl-user@example.test", permissions[0].Account.Address);
        Assert.AreEqual(200, permissions.get_ItemByDBID(30).Group.ID);
        Assert.AreEqual("ACL Group", permissions.get_ItemByDBID(30).Group.Name);

        var anyoneAccount = Assert.ThrowsExactly<COMException>(() => _ = permissions[1].Account);
        var anyoneGroup = Assert.ThrowsExactly<COMException>(() => _ = permissions[1].Group);
        Assert.AreEqual(DispEBadIndex, anyoneAccount.ErrorCode);
        Assert.AreEqual(DispEBadIndex, anyoneGroup.ErrorCode);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = permissions[3]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = permissions.get_ItemByDBID(40));
        var badName = Assert.ThrowsExactly<COMException>(() => _ = permissions.get_ItemByName("missing"));
        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);

        foreach (var mutation in new Action[]
                 {
                     () => permissions.Delete(0),
                     permissions.Refresh,
                     () => _ = permissions.Add(),
                     () => permissions.DeleteByDBID(10),
                     () => permissions[0].PermissionType = ComAclPermissionType.Group,
                     () => permissions[0].PermissionGroupID = 1,
                     () => permissions[0].PermissionAccountID = 1,
                     () => permissions[0].Value = 1,
                     () => permissions[0].set_Permission(ComAclPermission.Read, false),
                     permissions[0].Save,
                     permissions[0].Delete,
                     () => permissions.get_ItemByDBID(30).Group.Name = "Changed"
                 })
        {
            var error = Assert.ThrowsExactly<COMException>(mutation);
            Assert.AreEqual(ENotImplemented, error.ErrorCode);
        }
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        AccountAdministrationRuntimeHost.Configure(
            new FixedAccountAdministrationStore(
                new[]
                {
                    new AccountAdministrationSnapshot(100, 50, "acl-user@example.test", true, 0)
                }));
        GroupAdministrationRuntimeHost.Configure(
            new FixedGroupAdministrationStore(
                new[]
                {
                    new GroupAdministrationSnapshot(200, "ACL Group")
                }));

        var failReload = false;
        var reloads = 0;
        IInterfaceIMAPFolderPermissions permissions = IMAPFolderPermissions.CreateAuthorized(
            new[]
            {
                new ImapFolderPermissionAdministrationSnapshot(
                    10,
                    50,
                    (int)ComAclPermissionType.User,
                    0,
                    100,
                    (int)ComAclPermission.Lookup)
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
                    new ImapFolderPermissionAdministrationSnapshot(
                        20,
                        50,
                        (int)ComAclPermissionType.Anyone,
                        0,
                        0,
                        (int)ComAclPermission.Lookup),
                    new ImapFolderPermissionAdministrationSnapshot(
                        30,
                        50,
                        (int)ComAclPermissionType.Group,
                        200,
                        0,
                        (int)(ComAclPermission.Lookup | ComAclPermission.Read))
                };
            });

        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(100, permissions[0].Account.ID);

        permissions.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, permissions.Count);
        AssertPermission(
            permissions[0],
            20,
            50,
            ComAclPermissionType.Anyone,
            0,
            0,
            (int)ComAclPermission.Lookup);
        Assert.AreEqual(30, permissions.get_ItemByName("aclpermission-30").ID);
        Assert.AreEqual(200, permissions.get_ItemByDBID(30).Group.ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = permissions.get_ItemByDBID(10)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(permissions.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, permissions.Count);
        Assert.AreEqual("ACL Group", permissions.get_ItemByDBID(30).Group.Name);
    }

    private static void AssertPermission(
        IInterfaceIMAPFolderPermission permission,
        int id,
        int shareFolderId,
        ComAclPermissionType permissionType,
        int groupId,
        int accountId,
        int value)
    {
        Assert.AreEqual(id, permission.ID);
        Assert.AreEqual(shareFolderId, permission.ShareFolderID);
        Assert.AreEqual(permissionType, permission.PermissionType);
        Assert.AreEqual(groupId, permission.PermissionGroupID);
        Assert.AreEqual(accountId, permission.PermissionAccountID);
        Assert.AreEqual(value, permission.Value);
    }

    private sealed class TrackingLease(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }

    private sealed class PublicFolderPermissionStore :
        IImapFolderAdministrationStore,
        IImapFolderPermissionAdministrationMutationStore
    {
        private readonly ImapFolderAdministrationSnapshot _folder =
            new(50, 0, -1, "Public", true, 5, "2026-08-01 00:00:00");
        private IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> _permissions =
            Array.Empty<ImapFolderPermissionAdministrationSnapshot>();

        public int LastInsertFolderId { get; private set; }
        public int LastInsertAccountId { get; private set; }
        public (int FolderId, int PermissionId, int Type, int GroupId, int AccountId, int Value)? LastUpdate { get; private set; }

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetFoldersForAccountAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                accountId == 0 ? new[] { _folder } : Array.Empty<ImapFolderAdministrationSnapshot>());

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                accountId == 0 ? new[] { _folder } : Array.Empty<ImapFolderAdministrationSnapshot>());

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetChildFoldersAsync(
            int parentFolderId,
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                Array.Empty<ImapFolderAdministrationSnapshot>());

        public ValueTask<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>> GetFolderPermissionsAsync(
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>>(
                _permissions.Where(permission => permission.ShareFolderId == folderId).ToArray());

        public ValueTask<ImapFolderPermissionAdministrationSnapshot?> InsertFolderPermissionAsync(
            int folderId,
            int permissionType,
            int permissionGroupId,
            int permissionAccountId,
            int value,
            CancellationToken cancellationToken)
        {
            LastInsertFolderId = folderId;
            LastInsertAccountId = permissionAccountId;
            var inserted = new ImapFolderPermissionAdministrationSnapshot(
                501,
                folderId,
                permissionType,
                permissionGroupId,
                permissionAccountId,
                value);
            _permissions = _permissions.Append(inserted).ToArray();
            return ValueTask.FromResult<ImapFolderPermissionAdministrationSnapshot?>(inserted);
        }

        public ValueTask<bool> UpdateFolderPermissionAsync(
            int folderId,
            int permissionId,
            int permissionType,
            int permissionGroupId,
            int permissionAccountId,
            int value,
            CancellationToken cancellationToken)
        {
            var matches = _permissions
                .Where(permission => permission.Id == permissionId)
                .ToArray();
            if (matches.Length != 1 || matches[0].ShareFolderId != folderId)
            {
                return ValueTask.FromResult(false);
            }

            LastUpdate = (folderId, permissionId, permissionType, permissionGroupId, permissionAccountId, value);
            var updated = matches[0] with
            {
                PermissionType = permissionType,
                PermissionGroupId = permissionGroupId,
                PermissionAccountId = permissionAccountId,
                Value = value
            };
            _permissions = _permissions
                .Select(permission => permission.Id == permissionId ? updated : permission)
                .ToArray();
            return ValueTask.FromResult(true);
        }
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

    private sealed class FixedGroupAdministrationStore(IReadOnlyList<GroupAdministrationSnapshot> groups)
        : IGroupAdministrationStore
    {
        public ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> GetGroupsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<GroupAdministrationSnapshot>>(
                groups.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray());
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
}
