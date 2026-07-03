using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("A951C988-0D2C-42CA-A9D3-FE7A78F1AB25")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceIMAPFolderPermission
{
    [DispId(1)]
    int ID { get; }

    [DispId(3)]
    int ShareFolderID { get; }

    [DispId(4)]
    ComAclPermissionType PermissionType { get; set; }

    [DispId(5)]
    int PermissionGroupID { get; set; }

    [DispId(6)]
    int PermissionAccountID { get; set; }

    [DispId(7)]
    int Value { get; set; }

    [DispId(8)]
    [SpecialName]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool get_Permission(ComAclPermission permission);

    [DispId(8)]
    [SpecialName]
    void set_Permission(ComAclPermission permission, [MarshalAs(UnmanagedType.VariantBool)] bool value);

    [DispId(9)]
    void Save();

    [DispId(10)]
    void Delete();

    [DispId(11)]
    IInterfaceAccount Account { get; }

    [DispId(12)]
    IInterfaceGroup Group { get; }
}

[ComVisible(true)]
[Guid("CBE3FE9E-3642-4BA1-9BE0-6E766C0DE961")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceIMAPFolderPermissions
{
    [DispId(0)]
    IInterfaceIMAPFolderPermission this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    void Delete(int index);

    [DispId(3)]
    void Refresh();

    [DispId(4)]
    IInterfaceIMAPFolderPermission Add();

    [DispId(5)]
    [SpecialName]
    IInterfaceIMAPFolderPermission get_ItemByDBID(int databaseId);

    [DispId(6)]
    void DeleteByDBID(int databaseId);

    [DispId(7)]
    [SpecialName]
    IInterfaceIMAPFolderPermission get_ItemByName([MarshalAs(UnmanagedType.BStr)] string name);
}

[ComVisible(true)]
[Guid("A6B391A4-72C8-44AA-9480-9FB3BD593B46")]
[ProgId("hMailServer.IMAPFolderPermissions.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceIMAPFolderPermissions))]
public sealed class IMAPFolderPermissions : IInterfaceIMAPFolderPermissions
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>? _permissions;

    public IMAPFolderPermissions()
    {
    }

    private IMAPFolderPermissions(IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> permissions)
    {
        _permissions = permissions.ToArray();
    }

    public int Count => GetPermissions().Count;

    public IInterfaceIMAPFolderPermission this[int index]
    {
        get
        {
            var permissions = GetPermissions();
            if (index < 0 || index >= permissions.Count)
            {
                throw new COMException("IMAP folder permission index was outside the collection.", DispEBadIndex);
            }

            return IMAPFolderPermission.CreateAuthorized(permissions[index]);
        }
    }

    public IInterfaceIMAPFolderPermission get_ItemByDBID(int databaseId)
    {
        var match = GetPermissions().FirstOrDefault(permission => permission.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No IMAP folder permission with the specified database identifier exists.",
                DispEBadIndex)
            : IMAPFolderPermission.CreateAuthorized(match);
    }

    public IInterfaceIMAPFolderPermission get_ItemByName(string name)
    {
        var match = GetPermissions().FirstOrDefault(
            permission => string.Equals(
                LegacyName(permission),
                name ?? string.Empty,
                StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No IMAP folder permission with the specified name exists.", DispEBadIndex)
            : IMAPFolderPermission.CreateAuthorized(match);
    }

    public void Delete(int index) => Unavailable();

    public void Refresh() => Unavailable();

    public IInterfaceIMAPFolderPermission Add() => Unavailable<IInterfaceIMAPFolderPermission>();

    public void DeleteByDBID(int databaseId) => Unavailable();

    internal static IMAPFolderPermissions CreateAuthorized(
        IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return new IMAPFolderPermissions(permissions);
    }

    private static string LegacyName(ImapFolderPermissionAdministrationSnapshot permission) =>
        "ACLPermission-" + permission.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> GetPermissions()
    {
        return _permissions
            ?? throw new COMException(
                "IMAPFolderPermissions access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetPermissions();
        throw new COMException(
            "This IMAPFolderPermissions member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetPermissions();
        throw new COMException(
            "This IMAPFolderPermissions member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("D5800098-1033-4D83-9E06-94F6E1B557F9")]
[ProgId("hMailServer.IMAPFolderPermission.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceIMAPFolderPermission))]
public sealed class IMAPFolderPermission : IInterfaceIMAPFolderPermission
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly ImapFolderPermissionAdministrationSnapshot? _permission;

    public IMAPFolderPermission()
    {
    }

    private IMAPFolderPermission(ImapFolderPermissionAdministrationSnapshot permission)
    {
        _permission = permission;
    }

    public int ID => Snapshot.Id;

    public int ShareFolderID => Snapshot.ShareFolderId;

    public ComAclPermissionType PermissionType
    {
        get => (ComAclPermissionType)Snapshot.PermissionType;
        set => Unavailable();
    }

    public int PermissionGroupID { get => Snapshot.PermissionGroupId; set => Unavailable(); }

    public int PermissionAccountID { get => Snapshot.PermissionAccountId; set => Unavailable(); }

    public int Value { get => Snapshot.Value; set => Unavailable(); }

    public IInterfaceAccount Account =>
        AccountAdministrationRuntimeHost.CreateAuthorizedAccountByIdAdapter(Snapshot.PermissionAccountId);

    public IInterfaceGroup Group =>
        GroupAdministrationRuntimeHost.CreateAuthorizedGroupByIdAdapter(Snapshot.PermissionGroupId);

    public bool get_Permission(ComAclPermission permission) => (Snapshot.Value & (int)permission) != 0;

    public void set_Permission(ComAclPermission permission, bool value) => Unavailable();

    public void Save() => Unavailable();

    public void Delete() => Unavailable();

    internal static IMAPFolderPermission CreateAuthorized(
        ImapFolderPermissionAdministrationSnapshot permission) =>
        new(permission);

    private ImapFolderPermissionAdministrationSnapshot Snapshot =>
        _permission ?? throw new COMException(
            "IMAPFolderPermission access requires an authenticated server administrator.",
            EAccessDenied);

    private T Unavailable<T>()
    {
        _ = Snapshot;
        throw new COMException(
            "This IMAPFolderPermission member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This IMAPFolderPermission member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}
