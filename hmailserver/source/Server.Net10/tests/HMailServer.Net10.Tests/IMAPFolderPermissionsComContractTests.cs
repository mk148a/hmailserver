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
        var permissionsDeleteError = Assert.ThrowsExactly<COMException>(() => new IMAPFolderPermissions().DeleteByDBID(10));
        var permissionError = Assert.ThrowsExactly<COMException>(() => _ = new IMAPFolderPermission().ID);

        Assert.AreEqual(EAccessDenied, permissionsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, permissionsRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, permissionsDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, permissionError.ErrorCode);
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
                     () => permissions[0].Account.Address = "changed@example.test",
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
