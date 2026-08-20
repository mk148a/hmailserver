using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class PublicFolderAclHolderResolverTests
{
    private static readonly IReadOnlyList<AccountAdministrationSnapshot> Accounts =
    [
        new AccountAdministrationSnapshot(42, 7, "user@example.test", true, 0),
        new AccountAdministrationSnapshot(43, 8, "other@example.test", true, 0)
    ];

    private static readonly IReadOnlyList<GroupAdministrationSnapshot> Groups =
    [
        new GroupAdministrationSnapshot(77, "Editors"),
        new GroupAdministrationSnapshot(78, "Reviewers")
    ];

    [TestMethod]
    public void Resolve_UserGroupAndAnyone_MapLegacyHolderShapes()
    {
        var user = PublicFolderAclHolderResolver.Resolve(
            new RestoreFolderPermissionEntry(0, 3, "USER@example.test"),
            Accounts,
            Groups);
        var group = PublicFolderAclHolderResolver.Resolve(
            new RestoreFolderPermissionEntry(1, 1024, "editors"),
            Accounts,
            Groups);
        var anyone = PublicFolderAclHolderResolver.Resolve(
            new RestoreFolderPermissionEntry(2, 2047, "arbitrary-holder-text"),
            Accounts,
            Groups);

        Assert.AreEqual(new ResolvedPublicFolderAclPermission(0, 0, 42, 3), user);
        Assert.AreEqual(new ResolvedPublicFolderAclPermission(1, 77, 0, 1024), group);
        Assert.AreEqual(new ResolvedPublicFolderAclPermission(2, 0, 0, 2047), anyone);
    }

    [TestMethod]
    public void Resolve_UnresolvedUserAndGroupFailClosed()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => PublicFolderAclHolderResolver.Resolve(
            new RestoreFolderPermissionEntry(0, 1, "missing@example.test"),
            Accounts,
            Groups));
        Assert.ThrowsExactly<InvalidDataException>(() => PublicFolderAclHolderResolver.Resolve(
            new RestoreFolderPermissionEntry(1, 1, "MissingGroup"),
            Accounts,
            Groups));
    }

    [TestMethod]
    public void Resolve_InvalidTypeRightsAndHolderFailClosed()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => PublicFolderAclHolderResolver.Resolve(
            new RestoreFolderPermissionEntry(9, 1, "Anyone"),
            Accounts,
            Groups));
        Assert.ThrowsExactly<InvalidDataException>(() => PublicFolderAclHolderResolver.Resolve(
            new RestoreFolderPermissionEntry(0, 2048, "user@example.test"),
            Accounts,
            Groups));
        Assert.ThrowsExactly<InvalidDataException>(() => PublicFolderAclHolderResolver.Resolve(
            new RestoreFolderPermissionEntry(0, 1, " "),
            Accounts,
            Groups));
    }

    [TestMethod]
    public void Resolve_DuplicateHolderMatchesFailClosed()
    {
        var duplicateAccounts = Accounts.Append(
            new AccountAdministrationSnapshot(99, 9, "user@example.test", true, 0)).ToArray();
        var duplicateGroups = Groups.Append(new GroupAdministrationSnapshot(99, "Editors")).ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => PublicFolderAclHolderResolver.Resolve(
            new RestoreFolderPermissionEntry(0, 1, "user@example.test"),
            duplicateAccounts,
            Groups));
        Assert.ThrowsExactly<InvalidDataException>(() => PublicFolderAclHolderResolver.Resolve(
            new RestoreFolderPermissionEntry(1, 1, "Editors"),
            Accounts,
            duplicateGroups));
    }
}
