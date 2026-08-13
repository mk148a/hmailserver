using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("328B16A7-8314-4398-B506-90937569EDBA")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceIMAPFolders
{
    [DispId(0)]
    IInterfaceIMAPFolder this[int index] { get; }

    [DispId(1)]
    [SpecialName]
    IInterfaceIMAPFolder get_ItemByDBID(int databaseId);

    [DispId(2)]
    [SpecialName]
    IInterfaceIMAPFolder get_ItemByName([MarshalAs(UnmanagedType.BStr)] string name);

    [DispId(3)]
    int Count { get; }

    [DispId(4)]
    IInterfaceIMAPFolder Add([MarshalAs(UnmanagedType.BStr)] string name);

    [DispId(5)]
    void DeleteByDBID(int databaseId);
}

[ComVisible(true)]
[Guid("6EB9E09E-EBE2-4BD7-A8C5-3499257DEB0B")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceIMAPFolder
{
    [DispId(0)]
    int ID { get; }

    [DispId(1)]
    string Name { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(2)]
    bool Subscribed
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(3)]
    IInterfaceMessages Messages { get; }

    [DispId(4)]
    IInterfaceIMAPFolders SubFolders { get; }

    [DispId(6)]
    void Save();

    [DispId(7)]
    int ParentID { get; }

    [DispId(8)]
    IInterfaceIMAPFolderPermissions Permissions { get; }

    [DispId(9)]
    void Delete();

    [DispId(10)]
    int CurrentUID { get; }

    [DispId(11)]
    string CreationTime { [return: MarshalAs(UnmanagedType.BStr)] get; }
}

[ComVisible(true)]
[Guid("A0AAF31A-570A-4B78-BDAB-4C33E34BE85F")]
[ProgId("hMailServer.IMAPFolders.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceIMAPFolders))]
public sealed class IMAPFolders : IInterfaceIMAPFolders
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ELegacyComError = unchecked((int)0x800403E9);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<ImapFolderAdministrationSnapshot>? _folders;
    private readonly ImapFolderAdministrationState? _state;
    private readonly int _accountId;
    private readonly int _parentFolderId;
    private readonly Func<bool>? _isAuthenticated;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;

    public IMAPFolders()
    {
    }

    private IMAPFolders(
        IReadOnlyList<ImapFolderAdministrationSnapshot> folders,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        _folders = folders.ToArray();
        _isAuthenticated = isAuthenticated;
        _authorizationLeaseFactory = authorizationLeaseFactory;
    }

    private IMAPFolders(
        ImapFolderAdministrationState state,
        int accountId,
        int parentFolderId,
        Func<bool>? isAuthenticated,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory)
    {
        _state = state;
        _accountId = accountId;
        _parentFolderId = parentFolderId;
        _isAuthenticated = isAuthenticated;
        _authorizationLeaseFactory = authorizationLeaseFactory;
    }

    public int Count => GetFolders().Count;

    internal static IMAPFolders CreateAuthorized(
        IReadOnlyList<ImapFolderAdministrationSnapshot> folders,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(folders);
        return new IMAPFolders(folders, isAuthenticated, authorizationLeaseFactory);
    }

    public IInterfaceIMAPFolder this[int index]
    {
        get
        {
            var folders = GetFolders();
            if (index < 0 || index >= folders.Count)
            {
                throw new COMException("IMAP folder index was outside the collection.", DispEBadIndex);
            }

            return _state is { } state
                ? IMAPFolder.CreateAuthorized(folders[index], state, _isAuthenticated, _authorizationLeaseFactory)
                : IMAPFolder.CreateAuthorized(folders[index], _isAuthenticated, _authorizationLeaseFactory);
        }
    }

    public IInterfaceIMAPFolder get_ItemByDBID(int databaseId)
    {
        var match = GetFolders().FirstOrDefault(folder => folder.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No IMAP folder with the specified database identifier exists.",
                DispEBadIndex)
            : _state is { } state
                ? IMAPFolder.CreateAuthorized(match, state, _isAuthenticated, _authorizationLeaseFactory)
                : IMAPFolder.CreateAuthorized(match, _isAuthenticated, _authorizationLeaseFactory);
    }

    public IInterfaceIMAPFolder get_ItemByName(string name)
    {
        var encodedName = LegacyModifiedUtf7.Encode(name ?? string.Empty);
        var match = GetFolders().FirstOrDefault(
            folder => string.Equals(folder.Name, encodedName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No IMAP folder with the specified name exists.", DispEBadIndex)
            : _state is { } state
                ? IMAPFolder.CreateAuthorized(match, state, _isAuthenticated, _authorizationLeaseFactory)
                : IMAPFolder.CreateAuthorized(match, _isAuthenticated, _authorizationLeaseFactory);
    }

    public IInterfaceIMAPFolder Add(string name)
    {
        EnsureAuthenticated();
        var folders = GetFolders();
        var encodedName = LegacyModifiedUtf7.Encode(name ?? string.Empty);
        if (folders.Any(folder =>
                string.Equals(folder.Name, encodedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new COMException("Folder with specified name already exists.", ELegacyComError);
        }

        if (_state is not { } state)
        {
            throw new COMException(
                "This IMAPFolders member is not implemented by the .NET 10 rewrite yet.",
                ENotImplemented);
        }

        using var authorizationLease = AcquireAuthorizationLease();
        var snapshot = ImapFolderAdministrationRuntimeHost.InsertAuthorized(
                _accountId,
                _parentFolderId,
                encodedName,
                subscribed: _accountId == 0)
            .GetAwaiter()
            .GetResult();
        if (snapshot.Id <= 0
            || snapshot.AccountId != _accountId
            || snapshot.ParentId != _parentFolderId
            || !string.Equals(snapshot.Name, encodedName, StringComparison.Ordinal))
        {
            throw new COMException(
                "IMAP folder insertion returned an object outside the owning collection.",
                ELegacyComError);
        }

        state.Append(snapshot);
        return IMAPFolder.CreateAuthorized(snapshot, state, _isAuthenticated, _authorizationLeaseFactory);
    }

    public void DeleteByDBID(int databaseId)
    {
        EnsureAuthenticated();
        var selected = GetFolders().FirstOrDefault(folder => folder.Id == databaseId);
        if (selected is null)
        {
            throw new COMException(
                "No IMAP folder with the specified database identifier exists.",
                DispEBadIndex);
        }

        if (_state is null)
        {
            Unavailable();
            return;
        }

        using var authorizationLease = AcquireAuthorizationLease();
        try
        {
            var result = ImapFolderAdministrationRuntimeHost.DeleteAuthorized(selected)
                .GetAwaiter()
                .GetResult();
            if (result.Succeeded)
            {
                _state.RemoveTree(selected);
            }
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the IMAP folder from the database.",
                EFail);
        }
    }

    private IReadOnlyList<ImapFolderAdministrationSnapshot> GetFolders()
    {
        if (_state is { } state)
        {
            return state.GetFolders()
                .Where(folder => folder.AccountId == _accountId && folder.ParentId == _parentFolderId)
                .ToArray();
        }

        return _folders
            ?? throw new COMException(
                "IMAPFolders access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "IMAP folder access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private IDisposable? AcquireAuthorizationLease()
    {
        if (_authorizationLeaseFactory is null)
        {
            return null;
        }

        return _authorizationLeaseFactory(CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            ?? throw new COMException(
                "IMAP folder access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetFolders();
        throw new COMException(
            "This IMAPFolders member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetFolders();
        throw new COMException(
            "This IMAPFolders member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    internal static IMAPFolders CreateAuthorized(
        ImapFolderAdministrationState state,
        int accountId,
        int parentFolderId,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null) =>
        new(state, accountId, parentFolderId, isAuthenticated, authorizationLeaseFactory);
}

[ComVisible(true)]
[Guid("9FCA085E-E475-4DEE-9D45-5519818DD6E0")]
[ProgId("hMailServer.IMAPFolder.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceIMAPFolder))]
public sealed class IMAPFolder : IInterfaceIMAPFolder
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ELegacyComError = unchecked((int)0x800403E9);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private ImapFolderAdministrationSnapshot? _folder;
    private readonly ImapFolderAdministrationState? _foldersState;
    private readonly Func<bool>? _isAuthenticated;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;
    private string? _stagedName;
    private bool? _stagedSubscribed;

    public IMAPFolder()
    {
    }

    private IMAPFolder(
        ImapFolderAdministrationSnapshot folder,
        ImapFolderAdministrationState? foldersState = null,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        _folder = folder;
        _foldersState = foldersState;
        _isAuthenticated = isAuthenticated;
        _authorizationLeaseFactory = authorizationLeaseFactory;
    }

    public int ID => Snapshot.Id;

    public string Name
    {
        get => LegacyModifiedUtf7.Decode(_stagedName ?? Snapshot.Name);
        set
        {
            _ = Snapshot;
            if (_foldersState is null)
            {
                Unavailable();
                return;
            }

            _stagedName = LegacyModifiedUtf7.Encode(value ?? string.Empty);
        }
    }

    public bool Subscribed
    {
        get => _stagedSubscribed ?? Snapshot.Subscribed;
        set
        {
            _ = Snapshot;
            if (_foldersState is null)
            {
                Unavailable();
                return;
            }

            _stagedSubscribed = value;
        }
    }

    public IInterfaceMessages Messages => MessageAdministrationRuntimeHost.CreateAuthorizedFolderAdapter(
        Snapshot.Id,
        Snapshot.AccountId,
        _isAuthenticated);

    public IInterfaceIMAPFolders SubFolders =>
        _foldersState is { } state
            ? IMAPFolders.CreateAuthorized(
                state,
                Snapshot.AccountId,
                Snapshot.Id,
                _isAuthenticated,
                _authorizationLeaseFactory)
            : ImapFolderAdministrationRuntimeHost.CreateAuthorizedChildAdapter(
                Snapshot.Id,
                Snapshot.AccountId,
                _isAuthenticated,
                _authorizationLeaseFactory);

    public int ParentID => Snapshot.ParentId;

    public IInterfaceIMAPFolderPermissions Permissions
    {
        get
        {
            var snapshot = Snapshot;
            if (snapshot.AccountId != 0)
            {
                throw new COMException(
                    "It is only possible to modify permissions for public folders.",
                    ELegacyComError);
            }

            return ImapFolderAdministrationRuntimeHost.CreateAuthorizedPermissionsAdapter(
                snapshot.Id,
                _isAuthenticated,
                _authorizationLeaseFactory);
        }
    }

    public int CurrentUID => Snapshot.CurrentUid;

    public string CreationTime => Snapshot.CreationTime;

    internal static IMAPFolder CreateAuthorized(
        ImapFolderAdministrationSnapshot folder,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null) =>
        new(folder, isAuthenticated: isAuthenticated, authorizationLeaseFactory: authorizationLeaseFactory);

    internal static IMAPFolder CreateAuthorized(
        ImapFolderAdministrationSnapshot folder,
        ImapFolderAdministrationState state,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null) =>
        new(folder, state, isAuthenticated, authorizationLeaseFactory);

    public void Save()
    {
        var snapshot = Snapshot;
        EnsureAuthenticated();
        if (_foldersState is not { } state)
        {
            Unavailable();
            return;
        }

        var updated = snapshot with
        {
            Name = _stagedName ?? snapshot.Name,
            Subscribed = _stagedSubscribed ?? snapshot.Subscribed
        };
        using var authorizationLease = AcquireAuthorizationLease();
        var saved = ImapFolderAdministrationRuntimeHost.UpdateAuthorized(updated)
            .GetAwaiter()
            .GetResult();
        if (!saved || !state.Replace(updated))
        {
            throw new COMException(
                "Failed to save the IMAP folder.",
                ELegacyComError);
        }

        _folder = updated;
        _stagedName = null;
        _stagedSubscribed = null;
    }

    public void Delete()
    {
        var snapshot = Snapshot;
        EnsureAuthenticated();
        if (_foldersState is not { } state)
        {
            Unavailable();
            return;
        }

        using var authorizationLease = AcquireAuthorizationLease();
        ImapFolderAdministrationDeletionResult result;
        try
        {
            result = ImapFolderAdministrationRuntimeHost.DeleteAuthorized(snapshot)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the IMAP folder from the database.",
                unchecked((int)0x80004005));
        }

        if (result.Succeeded)
        {
            state.RemoveTree(snapshot);
        }
    }

    private ImapFolderAdministrationSnapshot Snapshot =>
        _folder ?? throw new COMException(
            "IMAPFolder access requires an authenticated server administrator.",
            EAccessDenied);

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "IMAP folder access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private IDisposable? AcquireAuthorizationLease()
    {
        if (_authorizationLeaseFactory is null)
        {
            return null;
        }

        return _authorizationLeaseFactory(CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            ?? throw new COMException(
                "IMAP folder access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = Snapshot;
        throw new COMException(
            "This IMAPFolder member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This IMAPFolder member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
internal sealed class ImapFolderAdministrationState
{
    private readonly object _sync = new();
    private readonly Func<IReadOnlyList<ImapFolderAdministrationSnapshot>> _load;
    private IReadOnlyList<ImapFolderAdministrationSnapshot>? _snapshot;

    public ImapFolderAdministrationState(Func<IReadOnlyList<ImapFolderAdministrationSnapshot>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        _load = load;
    }

    public IReadOnlyList<ImapFolderAdministrationSnapshot> GetFolders()
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot is not null)
        {
            return snapshot;
        }

        lock (_sync)
        {
            snapshot = _snapshot;
            if (snapshot is null)
            {
                snapshot = _load();
                ArgumentNullException.ThrowIfNull(snapshot);
                _snapshot = snapshot.ToArray();
            }

            return _snapshot!;
        }
    }

    public void Append(ImapFolderAdministrationSnapshot folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        lock (_sync)
        {
            _snapshot = GetFolders().Append(folder).ToArray();
        }
    }

    public bool Replace(ImapFolderAdministrationSnapshot folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        lock (_sync)
        {
            var folders = GetFolders().ToArray();
            var index = Array.FindIndex(folders, candidate => candidate.Id == folder.Id);
            if (index < 0)
            {
                return false;
            }

            folders[index] = folder;
            _snapshot = folders;
            return true;
        }
    }

    public void RemoveTree(ImapFolderAdministrationSnapshot folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        lock (_sync)
        {
            var folders = GetFolders().ToArray();
            var removedIds = new HashSet<int> { folder.Id };
            var preserveRootInbox = folder.ParentId == -1
                && string.Equals(folder.Name, "INBOX", StringComparison.OrdinalIgnoreCase);

            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var candidate in folders)
                {
                    if (candidate.AccountId == folder.AccountId
                        && removedIds.Contains(candidate.ParentId)
                        && removedIds.Add(candidate.Id))
                    {
                        changed = true;
                    }
                }
            }

            _snapshot = folders
                .Where(candidate => !removedIds.Contains(candidate.Id)
                    || (preserveRootInbox && candidate.Id == folder.Id))
                .ToArray();
        }
    }

    public void RemoveAllExceptInbox(int accountId)
    {
        lock (_sync)
        {
            _snapshot = GetFolders()
                .Where(folder => folder.AccountId != accountId
                    || (folder.ParentId == -1
                        && string.Equals(folder.Name, "INBOX", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }
    }
}

[ComVisible(false)]
public static class ImapFolderAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IImapFolderAdministrationStore? _store;
    private static IImapFolderMessageFileDeletionRuntime? _messageFileDeletionRuntime;
    private static readonly ConcurrentDictionary<int, ImapFolderAdministrationState> _states = new();

    public static void Configure(
        IImapFolderAdministrationStore store,
        IImapFolderMessageFileDeletionRuntime? messageFileDeletionRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
        Volatile.Write(ref _messageFileDeletionRuntime, messageFileDeletionRuntime);
        _states.Clear();
    }

    internal static ImapFolderAdministrationState CreateAuthorizedState(int accountId) =>
        _states.GetOrAdd(accountId, CreateState);

    internal static async ValueTask<ImapFolderAdministrationSnapshot> InsertAuthorized(
        int accountId,
        int parentFolderId,
        string encodedName,
        bool subscribed)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer IMAP folder administration runtime has not been initialized.",
                CoENotInitialized);
        if (store is not IImapFolderAdministrationMutationStore mutationStore)
        {
            throw new COMException(
                "IMAP folder insertion is not available in the configured administration store.",
                unchecked((int)0x80004001));
        }

        return await mutationStore.InsertFolderAsync(
                accountId,
                parentFolderId,
                encodedName,
                subscribed,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    internal static async ValueTask<bool> UpdateAuthorized(
        ImapFolderAdministrationSnapshot folder)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer IMAP folder administration runtime has not been initialized.",
                CoENotInitialized);
        if (store is not IImapFolderAdministrationMutationStore mutationStore)
        {
            throw new COMException(
                "IMAP folder updates are not available in the configured administration store.",
                unchecked((int)0x80004001));
        }

        return await mutationStore.UpdateFolderAsync(folder, CancellationToken.None)
            .ConfigureAwait(false);
    }

    internal static async ValueTask<ImapFolderAdministrationDeletionResult> DeleteAuthorized(
        ImapFolderAdministrationSnapshot folder)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer IMAP folder administration runtime has not been initialized.",
                CoENotInitialized);
        if (store is not IImapFolderAdministrationDeletionStore deletionStore)
        {
            throw new COMException(
                "IMAP folder deletion is not available in the configured administration store.",
                unchecked((int)0x80004001));
        }

        var result = await deletionStore.DeleteFolderAsync(folder, CancellationToken.None)
            .ConfigureAwait(false);
        if (result.Succeeded)
        {
            try
            {
                _ = Volatile.Read(ref _messageFileDeletionRuntime)?.TryDeleteAll(result);
            }
            catch
            {
                // Legacy folder deletion keeps the database result authoritative when file cleanup fails.
            }
        }

        return result;
    }

    internal static async ValueTask<ImapFolderAdministrationDeletionResult> DeleteAllForAccountAuthorized(
        int accountId,
        int domainId,
        string accountAddress)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer IMAP folder administration runtime has not been initialized.",
                CoENotInitialized);
        if (store is not IImapFolderAdministrationDeletionStore deletionStore)
        {
            throw new COMException(
                "Account message deletion is not available in the configured administration store.",
                unchecked((int)0x80004001));
        }

        var result = await deletionStore
            .DeleteAllForAccountAsync(accountId, domainId, accountAddress, CancellationToken.None)
            .ConfigureAwait(false);
        if (result.Succeeded)
        {
            try
            {
                _ = Volatile.Read(ref _messageFileDeletionRuntime)?.TryDeleteAll(result);
            }
            catch
            {
                // Legacy account message deletion keeps the database result authoritative when file cleanup fails.
            }

            if (_states.TryGetValue(accountId, out var state))
            {
                state.RemoveAllExceptInbox(accountId);
            }
        }

        return result;
    }

    internal static async ValueTask<ImapFolderPermissionAdministrationSnapshot?> InsertPermissionAuthorized(
        int folderId,
        int permissionType,
        int permissionGroupId,
        int permissionAccountId,
        int value)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer IMAP folder administration runtime has not been initialized.",
                CoENotInitialized);
        if (store is not IImapFolderPermissionAdministrationMutationStore mutationStore)
        {
            throw new COMException(
                "IMAP folder permission insertion is not available in the configured administration store.",
                unchecked((int)0x80004001));
        }

        return await mutationStore.InsertFolderPermissionAsync(
                folderId,
                permissionType,
                permissionGroupId,
                permissionAccountId,
                value,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    internal static async ValueTask<bool> UpdatePermissionAuthorized(
        int folderId,
        int permissionId,
        int permissionType,
        int permissionGroupId,
        int permissionAccountId,
        int value)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer IMAP folder administration runtime has not been initialized.",
                CoENotInitialized);
        if (store is not IImapFolderPermissionAdministrationMutationStore mutationStore)
        {
            throw new COMException(
                "IMAP folder permission updates are not available in the configured administration store.",
                unchecked((int)0x80004001));
        }

        return await mutationStore.UpdateFolderPermissionAsync(
                folderId,
                permissionId,
                permissionType,
                permissionGroupId,
                permissionAccountId,
                value,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static ImapFolderAdministrationState CreateState(int accountId) =>
        new(() =>
        {
            var store = Volatile.Read(ref _store)
                ?? throw new COMException(
                    "The hMailServer IMAP folder administration runtime has not been initialized.",
                    CoENotInitialized);

            return store
                .GetFoldersForAccountAsync(accountId, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        });

    internal static IMAPFolders CreateAuthorizedAdapter(
        int accountId,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer IMAP folder administration runtime has not been initialized.",
                CoENotInitialized);

        var folders = store
            .GetRootFoldersAsync(accountId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return IMAPFolders.CreateAuthorized(folders, isAuthenticated, authorizationLeaseFactory);
    }

    internal static IMAPFolders CreateAuthorizedChildAdapter(
        int parentFolderId,
        int accountId,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer IMAP folder administration runtime has not been initialized.",
                CoENotInitialized);

        var folders = store
            .GetChildFoldersAsync(parentFolderId, accountId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return IMAPFolders.CreateAuthorized(folders, isAuthenticated, authorizationLeaseFactory);
    }

    internal static IMAPFolderPermissions CreateAuthorizedPermissionsAdapter(
        int folderId,
        Func<bool>? isAuthenticated = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer IMAP folder administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<ImapFolderPermissionAdministrationSnapshot> LoadPermissions() => store
            .GetFolderPermissionsAsync(folderId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        var permissionStore = store as IImapFolderPermissionAdministrationStore;
        ValueTask<bool> DeletePermissionAsync(int ownerFolderId, int permissionId) => permissionStore
            ?.DeleteFolderPermissionAsync(ownerFolderId, permissionId, CancellationToken.None)
            ?? ValueTask.FromException<bool>(new NotSupportedException());
        var permissionMutationStore = store as IImapFolderPermissionAdministrationMutationStore;
        ValueTask<ImapFolderPermissionAdministrationSnapshot?> InsertPermissionAsync(
            int permissionType,
            int permissionGroupId,
            int permissionAccountId,
            int value) => InsertPermissionAuthorized(
                folderId,
                permissionType,
                permissionGroupId,
                permissionAccountId,
                value);
        ValueTask<bool> UpdatePermissionAsync(
            ImapFolderPermissionAdministrationSnapshot permission,
            int permissionType,
            int permissionGroupId,
            int permissionAccountId,
            int value) => UpdatePermissionAuthorized(
                folderId,
                permission.Id,
                permissionType,
                permissionGroupId,
                permissionAccountId,
                value);

        return IMAPFolderPermissions.CreateAuthorized(
            folderId,
            LoadPermissions(),
            LoadPermissions,
            permissionStore is null ? null : DeletePermissionAsync,
            permissionMutationStore is null ? null : InsertPermissionAsync,
            permissionMutationStore is null ? null : UpdatePermissionAsync,
            isAuthenticated,
            authorizationLeaseFactory);
    }
}

internal static class LegacyModifiedUtf7
{
    private static readonly Encoding BigEndianUnicode =
        new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);

    public static string Encode(string value)
    {
        var output = new StringBuilder(value.Length);
        var position = 0;

        while (position < value.Length)
        {
            var current = value[position];
            if (!IsSpecial(current))
            {
                output.Append(current);
                if (current == '&')
                {
                    output.Append('-');
                }

                position++;
                continue;
            }

            var start = position;
            while (position < value.Length && IsSpecial(value[position]))
            {
                position++;
            }

            var bytes = BigEndianUnicode.GetBytes(value[start..position]);
            output.Append('&');
            output.Append(Convert.ToBase64String(bytes).TrimEnd('='));
            output.Append('-');
        }

        return output.ToString();
    }

    public static string Decode(string value)
    {
        var output = new StringBuilder(value.Length);

        for (var position = 0; position < value.Length; position++)
        {
            var current = value[position];
            if (IsSpecial(current))
            {
                return string.Empty;
            }

            if (current != '&')
            {
                output.Append(current);
                continue;
            }

            if (++position >= value.Length)
            {
                return string.Empty;
            }

            if (value[position] == '-')
            {
                output.Append('&');
                continue;
            }

            var end = value.IndexOf('-', position);
            if (end < 0)
            {
                return string.Empty;
            }

            var encoded = value[position..end];
            var padding = encoded.Length % 4;
            if (padding != 0)
            {
                encoded = encoded.PadRight(encoded.Length + 4 - padding, '=');
            }

            try
            {
                output.Append(BigEndianUnicode.GetString(Convert.FromBase64String(encoded)));
            }
            catch (FormatException)
            {
                return string.Empty;
            }
            catch (DecoderFallbackException)
            {
                return string.Empty;
            }

            position = end;
        }

        return output.ToString();
    }

    private static bool IsSpecial(char value) => value < 32 || value >= 127;
}
