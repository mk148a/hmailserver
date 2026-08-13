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
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private ImapFolderPermissionAdministrationSnapshot[]? _permissions;
    private readonly Func<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>>? _reload;
    private readonly int _folderId;
    private readonly Func<int, int, ValueTask<bool>>? _delete;
    private readonly Func<int, int, int, int, ValueTask<ImapFolderPermissionAdministrationSnapshot?>>? _insert;
    private readonly Func<ImapFolderPermissionAdministrationSnapshot, int, int, int, int, ValueTask<bool>>? _update;
    private readonly Func<bool>? _isAuthenticated;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;

    public IMAPFolderPermissions()
    {
    }

    private IMAPFolderPermissions(
        IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> permissions,
        Func<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>>? reload,
        int folderId = 0,
        Func<int, int, ValueTask<bool>>? delete = null,
        Func<int, int, int, int, ValueTask<ImapFolderPermissionAdministrationSnapshot?>>? insert = null,
        Func<ImapFolderPermissionAdministrationSnapshot, int, int, int, int, ValueTask<bool>>? update = null,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        _permissions = permissions.ToArray();
        _reload = reload;
        _folderId = folderId;
        _delete = delete;
        _insert = insert;
        _update = update;
        _isAuthenticated = isAuthenticated;
        _authorizationLeaseFactory = authorizationLeaseFactory;
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

            return IMAPFolderPermission.CreateAuthorized(
                permissions[index],
                _folderId,
                _delete is null ? null : DeleteSelectedAsync,
                _update is null || permissions[index].ShareFolderId != _folderId
                    ? null
                    : UpdateSelectedAsync,
                isAuthenticated: _isAuthenticated,
                authorizationLeaseFactory: _authorizationLeaseFactory);
        }
    }

    public IInterfaceIMAPFolderPermission get_ItemByDBID(int databaseId)
    {
        var match = GetPermissions().FirstOrDefault(permission => permission.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No IMAP folder permission with the specified database identifier exists.",
                DispEBadIndex)
            : IMAPFolderPermission.CreateAuthorized(
                match,
                _folderId,
                _delete is null ? null : DeleteSelectedAsync,
                _update is null || match.ShareFolderId != _folderId
                    ? null
                    : UpdateSelectedAsync,
                isAuthenticated: _isAuthenticated,
                authorizationLeaseFactory: _authorizationLeaseFactory);
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
            : IMAPFolderPermission.CreateAuthorized(
                match,
                _folderId,
                delete: null,
                update: _update is null || match.ShareFolderId != _folderId
                    ? null
                    : UpdateSelectedAsync,
                isAuthenticated: _isAuthenticated,
                authorizationLeaseFactory: _authorizationLeaseFactory);
    }

    public void Delete(int index)
    {
        var permissions = GetPermissions();
        if (index < 0 || index >= permissions.Count)
        {
            return;
        }

        var selected = permissions[index];
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        bool deleted;
        try
        {
            deleted = _delete(_folderId, selected.Id).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the IMAP folder permission from the database.",
                EFail);
        }

        if (deleted)
        {
            Volatile.Write(
                ref _permissions,
                permissions.Where(permission => !ReferenceEquals(permission, selected)).ToArray());
        }
    }

    public void Refresh()
    {
        _ = GetPermissions();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var permissions = _reload();
            ArgumentNullException.ThrowIfNull(permissions);
            Volatile.Write(ref _permissions, permissions.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of IMAP folder permissions from the database.",
                EFail);
        }
    }

    public IInterfaceIMAPFolderPermission Add()
    {
        _ = GetPermissions();
        if (_insert is null)
        {
            Unavailable();
        }

        return IMAPFolderPermission.CreateNew(_folderId, _insert!, AppendInserted, _isAuthenticated);
    }

    public void DeleteByDBID(int databaseId)
    {
        var permissions = GetPermissions();
        var selected = permissions.FirstOrDefault(permission => permission.Id == databaseId);
        if (selected is null)
        {
            return;
        }

        if (_delete is null)
        {
            Unavailable();
            return;
        }

        bool deleted;
        try
        {
            deleted = _delete(_folderId, selected.Id).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the IMAP folder permission from the database.",
                EFail);
        }

        if (deleted)
        {
            Volatile.Write(
                ref _permissions,
                permissions.Where(permission => !ReferenceEquals(permission, selected)).ToArray());
        }
    }

    private async ValueTask DeleteSelectedAsync(int folderId, int permissionId)
    {
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        var permissions = GetPermissions();
        if (!permissions.Any(permission => permission.ShareFolderId == folderId && permission.Id == permissionId))
        {
            return;
        }

        if (await _delete(folderId, permissionId).ConfigureAwait(false))
        {
            Volatile.Write(
                ref _permissions,
                permissions
                    .Where(permission => permission.ShareFolderId != folderId || permission.Id != permissionId)
                    .ToArray());
        }
    }

    private async ValueTask<bool> UpdateSelectedAsync(
        ImapFolderPermissionAdministrationSnapshot selected,
        int permissionType,
        int permissionGroupId,
        int permissionAccountId,
        int value)
    {
        if (_update is null)
        {
            Unavailable();
            return false;
        }

        var permissions = GetPermissions();
        var matches = permissions
            .Where(permission => permission.Id == selected.Id)
            .ToArray();
        if (matches.Length != 1
            || !Equals(matches[0], selected)
            || selected.ShareFolderId != _folderId)
        {
            return false;
        }

        if (!await _update(
                selected,
                permissionType,
                permissionGroupId,
                permissionAccountId,
                value)
            .ConfigureAwait(false))
        {
            return false;
        }

        var updated = selected with
        {
            PermissionType = permissionType,
            PermissionGroupId = permissionGroupId,
            PermissionAccountId = permissionAccountId,
            Value = value
        };
        var current = GetPermissions();
        var currentMatches = current
            .Where(permission => permission.Id == selected.Id)
            .ToArray();
        if (currentMatches.Length != 1 || !Equals(currentMatches[0], selected))
        {
            return false;
        }

        Volatile.Write(
            ref _permissions,
            current.Select(permission => Equals(permission, selected) ? updated : permission).ToArray());
        return true;
    }

    internal static IMAPFolderPermissions CreateAuthorized(
        IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> permissions,
        Func<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>>? reload = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return new IMAPFolderPermissions(permissions, reload);
    }

    internal static IMAPFolderPermissions CreateAuthorized(
        int folderId,
        IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> permissions,
        Func<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>> reload,
        Func<int, int, ValueTask<bool>>? delete,
        Func<int, int, int, int, ValueTask<ImapFolderPermissionAdministrationSnapshot?>>? insert = null,
        Func<ImapFolderPermissionAdministrationSnapshot, int, int, int, int, ValueTask<bool>>? update = null,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(reload);
        return new IMAPFolderPermissions(
            permissions,
            reload,
            folderId,
            delete,
            insert,
            update,
            isAuthenticated,
            authorizationLeaseFactory);
    }

    private void AppendInserted(ImapFolderPermissionAdministrationSnapshot permission)
    {
        var permissions = GetPermissions();
        Volatile.Write(ref _permissions, permissions.Append(permission).ToArray());
    }

    private static string LegacyName(ImapFolderPermissionAdministrationSnapshot permission) =>
        "ACLPermission-" + permission.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> GetPermissions()
    {
        return Volatile.Read(ref _permissions)
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
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private ImapFolderPermissionAdministrationSnapshot? _permission;
    private readonly int _folderId;
    private readonly Func<int, int, ValueTask>? _delete;
    private readonly Func<int, int, int, int, ValueTask<ImapFolderPermissionAdministrationSnapshot?>>? _insert;
    private readonly Func<ImapFolderPermissionAdministrationSnapshot, int, int, int, int, ValueTask<bool>>? _update;
    private readonly Action<ImapFolderPermissionAdministrationSnapshot>? _append;
    private readonly Func<bool>? _isAuthenticated;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;
    private int? _stagedPermissionType;
    private int? _stagedPermissionGroupId;
    private int? _stagedPermissionAccountId;
    private int? _stagedValue;

    public IMAPFolderPermission()
    {
    }

    private IMAPFolderPermission(
        ImapFolderPermissionAdministrationSnapshot permission,
        int folderId = 0,
        Func<int, int, ValueTask>? delete = null,
        Func<int, int, int, int, ValueTask<ImapFolderPermissionAdministrationSnapshot?>>? insert = null,
        Func<ImapFolderPermissionAdministrationSnapshot, int, int, int, int, ValueTask<bool>>? update = null,
        Action<ImapFolderPermissionAdministrationSnapshot>? append = null,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        _permission = permission;
        _folderId = folderId;
        _delete = delete;
        _insert = insert;
        _update = update;
        _append = append;
        _isAuthenticated = isAuthenticated;
        _authorizationLeaseFactory = authorizationLeaseFactory;
    }

    public int ID => Snapshot.Id;

    public int ShareFolderID => Snapshot.ShareFolderId;

    public ComAclPermissionType PermissionType
    {
        get => (ComAclPermissionType)(_stagedPermissionType ?? Snapshot.PermissionType);
        set
        {
            EnsureMutable();
            _stagedPermissionType = (int)value;
        }
    }

    public int PermissionGroupID
    {
        get => _stagedPermissionGroupId ?? Snapshot.PermissionGroupId;
        set
        {
            EnsureMutable();
            _stagedPermissionGroupId = value;
        }
    }

    public int PermissionAccountID
    {
        get => _stagedPermissionAccountId ?? Snapshot.PermissionAccountId;
        set
        {
            EnsureMutable();
            _stagedPermissionAccountId = value;
        }
    }

    public int Value
    {
        get => _stagedValue ?? Snapshot.Value;
        set
        {
            EnsureMutable();
            _stagedValue = value;
        }
    }

    public IInterfaceAccount Account =>
        AccountAdministrationRuntimeHost.CreateAuthorizedAccountByIdAdapter(
            Snapshot.PermissionAccountId,
            _isAuthenticated,
            _authorizationLeaseFactory);

    public IInterfaceGroup Group =>
        GroupAdministrationRuntimeHost.CreateAuthorizedGroupByIdAdapter(
            Snapshot.PermissionGroupId,
            _isAuthenticated,
            _authorizationLeaseFactory);

    public bool get_Permission(ComAclPermission permission)
    {
        var snapshot = Snapshot;
        if (IsNew)
        {
            EnsurePermissionFlag(permission);
        }

        return ((_stagedValue ?? snapshot.Value) & (int)permission) != 0;
    }

    public void set_Permission(ComAclPermission permission, bool value)
    {
        EnsureMutable();
        EnsurePermissionFlag(permission);
        _stagedValue = value ? Value | (int)permission : Value & ~(int)permission;
    }

    public void Save()
    {
        var permission = Snapshot;
        if (permission.Id != 0)
        {
            SaveExisting(permission);
            return;
        }

        if (_insert is null || _append is null)
        {
            Unavailable();
            return;
        }

        var permissionType = _stagedPermissionType ?? permission.PermissionType;
        var permissionGroupId = _stagedPermissionGroupId ?? permission.PermissionGroupId;
        var permissionAccountId = _stagedPermissionAccountId ?? permission.PermissionAccountId;
        var value = _stagedValue ?? permission.Value;
        if (!IsValidHolder(permissionType, permissionGroupId, permissionAccountId))
        {
            FailSave();
        }

        ImapFolderPermissionAdministrationSnapshot? inserted;
        try
        {
            inserted = _insert(permissionType, permissionGroupId, permissionAccountId, value)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception)
        {
            FailSave();
            return;
        }

        if (inserted is null
            || inserted.Id <= 0
            || inserted.ShareFolderId != _folderId
            || inserted.PermissionType != permissionType
            || inserted.PermissionGroupId != permissionGroupId
            || inserted.PermissionAccountId != permissionAccountId
            || inserted.Value != value
            || !IsValidHolder(inserted.PermissionType, inserted.PermissionGroupId, inserted.PermissionAccountId))
        {
            FailSave();
        }

        var validatedInserted = inserted!;
        try
        {
            _append(validatedInserted);
        }
        catch (Exception)
        {
            FailSave();
            return;
        }

        _permission = validatedInserted;
        _stagedPermissionType = null;
        _stagedPermissionGroupId = null;
        _stagedPermissionAccountId = null;
        _stagedValue = null;
    }

    private void SaveExisting(ImapFolderPermissionAdministrationSnapshot permission)
    {
        if (_update is null)
        {
            Unavailable();
            return;
        }

        var permissionType = _stagedPermissionType ?? permission.PermissionType;
        var permissionGroupId = _stagedPermissionGroupId ?? permission.PermissionGroupId;
        var permissionAccountId = _stagedPermissionAccountId ?? permission.PermissionAccountId;
        var value = _stagedValue ?? permission.Value;
        if (!IsValidHolder(permissionType, permissionGroupId, permissionAccountId))
        {
            FailSave();
        }

        bool updated;
        try
        {
            updated = _update(
                    permission,
                    permissionType,
                    permissionGroupId,
                    permissionAccountId,
                    value)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception)
        {
            FailSave();
            return;
        }

        if (!updated)
        {
            FailSave();
        }

        _permission = permission with
        {
            PermissionType = permissionType,
            PermissionGroupId = permissionGroupId,
            PermissionAccountId = permissionAccountId,
            Value = value
        };
        _stagedPermissionType = null;
        _stagedPermissionGroupId = null;
        _stagedPermissionAccountId = null;
        _stagedValue = null;
    }

    public void Delete()
    {
        var permission = Snapshot;
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _delete(_folderId, permission.Id).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the IMAP folder permission from the database.",
                EFail);
        }
    }

    internal static IMAPFolderPermission CreateAuthorized(
        ImapFolderPermissionAdministrationSnapshot permission) =>
        new(permission);

    internal static IMAPFolderPermission CreateAuthorized(
        ImapFolderPermissionAdministrationSnapshot permission,
        int folderId) =>
        new(permission, folderId);

    internal static IMAPFolderPermission CreateAuthorized(
        ImapFolderPermissionAdministrationSnapshot permission,
        int folderId,
        Func<int, int, ValueTask>? delete,
        Func<ImapFolderPermissionAdministrationSnapshot, int, int, int, int, ValueTask<bool>>? update = null,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null) =>
        new(
            permission,
            folderId,
            delete,
            update: update,
            isAuthenticated: isAuthenticated,
            authorizationLeaseFactory: authorizationLeaseFactory);

    internal static IMAPFolderPermission CreateNew(
        int folderId,
        Func<int, int, int, int, ValueTask<ImapFolderPermissionAdministrationSnapshot?>> insert,
        Action<ImapFolderPermissionAdministrationSnapshot> append,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null) =>
        new(
            new ImapFolderPermissionAdministrationSnapshot(
                0,
                folderId,
                (int)ComAclPermissionType.User,
                0,
                0,
                0),
            folderId,
            insert: insert,
            append: append,
            isAuthenticated: isAuthenticated,
            authorizationLeaseFactory: authorizationLeaseFactory);

    private bool IsNew => Snapshot.Id == 0 && _insert is not null && _append is not null;

    private void EnsureMutable()
    {
        _ = Snapshot;
        if (!IsNew && _update is null)
        {
            Unavailable();
        }
    }

    private static void EnsurePermissionFlag(ComAclPermission permission)
    {
        var value = (int)permission;
        if (value is not (1 or 2 or 4 or 8 or 16 or 32 or 64 or 128 or 256 or 512 or 1024))
        {
            FailSave();
        }
    }

    private static bool IsValidHolder(int permissionType, int permissionGroupId, int permissionAccountId) =>
        permissionType switch
        {
            (int)ComAclPermissionType.User => permissionAccountId != 0 && permissionGroupId == 0,
            (int)ComAclPermissionType.Group => permissionGroupId != 0 && permissionAccountId == 0,
            (int)ComAclPermissionType.Anyone => permissionGroupId == 0 && permissionAccountId == 0,
            _ => false
        };

    private static void FailSave() => throw new COMException(
        "It was not possible to save the IMAP folder permission.",
        EFail);

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
