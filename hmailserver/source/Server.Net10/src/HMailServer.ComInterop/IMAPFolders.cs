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
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<ImapFolderAdministrationSnapshot>? _folders;
    private readonly ImapFolderAdministrationState? _state;
    private readonly int _accountId;
    private readonly int _parentFolderId;

    public IMAPFolders()
    {
    }

    private IMAPFolders(IReadOnlyList<ImapFolderAdministrationSnapshot> folders)
    {
        _folders = folders.ToArray();
    }

    private IMAPFolders(
        ImapFolderAdministrationState state,
        int accountId,
        int parentFolderId)
    {
        _state = state;
        _accountId = accountId;
        _parentFolderId = parentFolderId;
    }

    public int Count => GetFolders().Count;

    internal static IMAPFolders CreateAuthorized(IReadOnlyList<ImapFolderAdministrationSnapshot> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        return new IMAPFolders(folders);
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
                ? IMAPFolder.CreateAuthorized(folders[index], state)
                : IMAPFolder.CreateAuthorized(folders[index]);
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
                ? IMAPFolder.CreateAuthorized(match, state)
                : IMAPFolder.CreateAuthorized(match);
    }

    public IInterfaceIMAPFolder get_ItemByName(string name)
    {
        var encodedName = LegacyModifiedUtf7.Encode(name ?? string.Empty);
        var match = GetFolders().FirstOrDefault(
            folder => string.Equals(folder.Name, encodedName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No IMAP folder with the specified name exists.", DispEBadIndex)
            : _state is { } state
                ? IMAPFolder.CreateAuthorized(match, state)
                : IMAPFolder.CreateAuthorized(match);
    }

    public IInterfaceIMAPFolder Add(string name) => Unavailable<IInterfaceIMAPFolder>();

    public void DeleteByDBID(int databaseId) => Unavailable();

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
        int parentFolderId) =>
        new(state, accountId, parentFolderId);
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

    private readonly ImapFolderAdministrationSnapshot? _folder;
    private readonly ImapFolderAdministrationState? _foldersState;

    public IMAPFolder()
    {
    }

    private IMAPFolder(
        ImapFolderAdministrationSnapshot folder,
        ImapFolderAdministrationState? foldersState = null)
    {
        _folder = folder;
        _foldersState = foldersState;
    }

    public int ID => Snapshot.Id;

    public string Name { get => LegacyModifiedUtf7.Decode(Snapshot.Name); set => Unavailable(); }

    public bool Subscribed { get => Snapshot.Subscribed; set => Unavailable(); }

    public IInterfaceMessages Messages => MessageAdministrationRuntimeHost.CreateAuthorizedFolderAdapter(Snapshot.Id);

    public IInterfaceIMAPFolders SubFolders =>
        _foldersState is { } state
            ? IMAPFolders.CreateAuthorized(state, Snapshot.AccountId, Snapshot.Id)
            : ImapFolderAdministrationRuntimeHost.CreateAuthorizedChildAdapter(Snapshot.Id, Snapshot.AccountId);

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

            return ImapFolderAdministrationRuntimeHost.CreateAuthorizedPermissionsAdapter(snapshot.Id);
        }
    }

    public int CurrentUID => Snapshot.CurrentUid;

    public string CreationTime => Snapshot.CreationTime;

    internal static IMAPFolder CreateAuthorized(ImapFolderAdministrationSnapshot folder) => new(folder);

    internal static IMAPFolder CreateAuthorized(
        ImapFolderAdministrationSnapshot folder,
        ImapFolderAdministrationState state) =>
        new(folder, state);

    public void Save() => Unavailable();

    public void Delete() => Unavailable();

    private ImapFolderAdministrationSnapshot Snapshot =>
        _folder ?? throw new COMException(
            "IMAPFolder access requires an authenticated server administrator.",
            EAccessDenied);

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
    private readonly Lazy<IReadOnlyList<ImapFolderAdministrationSnapshot>> _folders;

    public ImapFolderAdministrationState(Func<IReadOnlyList<ImapFolderAdministrationSnapshot>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        _folders = new(
            () =>
            {
                var folders = load();
                ArgumentNullException.ThrowIfNull(folders);
                return folders.ToArray();
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyList<ImapFolderAdministrationSnapshot> GetFolders() => _folders.Value;
}

[ComVisible(false)]
public static class ImapFolderAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IImapFolderAdministrationStore? _store;
    private static readonly ConcurrentDictionary<int, ImapFolderAdministrationState> _states = new();

    public static void Configure(IImapFolderAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
        _states.Clear();
    }

    internal static ImapFolderAdministrationState CreateAuthorizedState(int accountId) =>
        _states.GetOrAdd(accountId, CreateState);

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

    internal static IMAPFolders CreateAuthorizedAdapter(int accountId)
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

        return IMAPFolders.CreateAuthorized(folders);
    }

    internal static IMAPFolders CreateAuthorizedChildAdapter(int parentFolderId, int accountId)
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

        return IMAPFolders.CreateAuthorized(folders);
    }

    internal static IMAPFolderPermissions CreateAuthorizedPermissionsAdapter(int folderId)
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

        return IMAPFolderPermissions.CreateAuthorized(LoadPermissions(), LoadPermissions);
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
