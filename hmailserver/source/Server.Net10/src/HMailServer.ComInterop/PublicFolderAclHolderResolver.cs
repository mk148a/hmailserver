using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

public sealed record ResolvedPublicFolderAclPermission(
    int PermissionType,
    int PermissionGroupId,
    int PermissionAccountId,
    int Rights);

public static class PublicFolderAclHolderResolver
{
    public static ResolvedPublicFolderAclPermission Resolve(
        RestoreFolderPermissionEntry entry,
        IReadOnlyList<AccountAdministrationSnapshot> accounts,
        IReadOnlyList<GroupAdministrationSnapshot> groups)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(groups);

        if (entry.PermissionType is < 0 or > 2)
        {
            throw new InvalidDataException("The public-folder ACL permission type is unsupported.");
        }

        if (entry.Rights is < 0 or > 2047)
        {
            throw new InvalidDataException("The public-folder ACL rights value is outside the legacy range.");
        }

        if (string.IsNullOrWhiteSpace(entry.Holder))
        {
            throw new InvalidDataException("The public-folder ACL holder is empty.");
        }

        return entry.PermissionType switch
        {
            0 => ResolveAccount(entry, accounts),
            1 => ResolveGroup(entry, groups),
            2 => new ResolvedPublicFolderAclPermission(2, 0, 0, entry.Rights),
            _ => throw new InvalidDataException("The public-folder ACL permission type is unsupported.")
        };
    }

    private static ResolvedPublicFolderAclPermission ResolveAccount(
        RestoreFolderPermissionEntry entry,
        IReadOnlyList<AccountAdministrationSnapshot> accounts)
    {
        var matches = accounts
            .Where(account => string.Equals(account.Address, entry.Holder, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1 || matches[0].Id <= 0)
        {
            throw new InvalidDataException(
                $"The public-folder ACL user holder '{entry.Holder}' could not be resolved uniquely.");
        }

        return new ResolvedPublicFolderAclPermission(0, 0, matches[0].Id, entry.Rights);
    }

    private static ResolvedPublicFolderAclPermission ResolveGroup(
        RestoreFolderPermissionEntry entry,
        IReadOnlyList<GroupAdministrationSnapshot> groups)
    {
        var matches = groups
            .Where(group => string.Equals(group.Name, entry.Holder, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1 || matches[0].Id <= 0)
        {
            throw new InvalidDataException(
                $"The public-folder ACL group holder '{entry.Holder}' could not be resolved uniquely.");
        }

        return new ResolvedPublicFolderAclPermission(1, matches[0].Id, 0, entry.Rights);
    }
}
