using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("BC84454B-FCE1-41FA-A3DD-2C57F61D4310")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceBackup
{
    [DispId(1)]
    void StartRestore();

    [DispId(2)]
    bool ContainsSettings { [return: MarshalAs(UnmanagedType.VariantBool)] get; }

    [DispId(3)]
    bool ContainsDomains { [return: MarshalAs(UnmanagedType.VariantBool)] get; }

    [DispId(4)]
    bool ContainsMessages { [return: MarshalAs(UnmanagedType.VariantBool)] get; }

    [DispId(5)]
    bool RestoreSettings
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(6)]
    bool RestoreDomains
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(7)]
    bool RestoreMessages
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }
}

[ComVisible(true)]
[Guid("B088FED1-A784-4CDB-ADDF-E7332CB7F72F")]
[ProgId("hMailServer.Backup.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceBackup))]
public sealed class Backup : IInterfaceBackup
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int SettingsFlag = 1;
    private const int DomainsFlag = 2;
    private const int MessagesFlag = 4;

    private readonly bool _authorized;
    private readonly Func<bool>? _authorizationGuard;
    private readonly int _containsOptions;
    private readonly string? _archivePath;
    private readonly Action<Backup>? _startRestore;
    private readonly BackupArchiveIdentity? _archiveIdentity;
    private BackupArchiveBinding? _archiveBinding;
    private int _restoreOptions;

    public Backup()
    {
    }

    private Backup(
        int containsOptions,
        string? archivePath,
        Action<Backup>? startRestore,
        Func<bool>? authorizationGuard,
        BackupArchiveIdentity? archiveIdentity,
        BackupArchiveBinding? archiveBinding,
        BackupDataDirectoryIdentity? rawDataBackupIdentity)
    {
        _authorized = true;
        _authorizationGuard = authorizationGuard;
        _containsOptions = containsOptions;
        _archivePath = archivePath;
        _startRestore = startRestore;
        _archiveIdentity = archiveIdentity;
        _archiveBinding = archiveBinding;
        RawDataBackupIdentity = rawDataBackupIdentity;
    }

    public bool ContainsSettings => HasContainsFlag(SettingsFlag);

    public bool ContainsDomains => HasContainsFlag(DomainsFlag);

    public bool ContainsMessages => HasContainsFlag(MessagesFlag);

    public bool RestoreSettings
    {
        get => HasRestoreFlag(SettingsFlag);
        set => SetRestoreFlag(SettingsFlag, value);
    }

    public bool RestoreDomains
    {
        get => HasRestoreFlag(DomainsFlag);
        set => SetRestoreFlag(DomainsFlag, value);
    }

    public bool RestoreMessages
    {
        get => HasRestoreFlag(MessagesFlag);
        set => SetRestoreFlag(MessagesFlag, value);
    }

    public void StartRestore()
    {
        EnsureAuthorized();
        if (_startRestore is null)
        {
            throw new COMException(
                "This Backup member is not implemented by the .NET 10 rewrite yet.",
                ENotImplemented);
        }

        _startRestore(this);
    }

    internal static Backup CreateAuthorized(
        int containsOptions,
        string? archivePath = null,
        Action<Backup>? startRestore = null,
        Func<bool>? authorizationGuard = null,
        BackupArchiveIdentity? archiveIdentity = null,
        BackupArchiveBinding? archiveBinding = null,
        BackupDataDirectoryIdentity? rawDataBackupIdentity = null) =>
        new(
            containsOptions,
            archivePath,
            startRestore,
            authorizationGuard,
            archiveIdentity,
            archiveBinding,
            rawDataBackupIdentity);

    internal string ArchivePath => _archiveBinding?.ArchivePath ?? _archivePath
        ?? throw new COMException(
            "This Backup member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);

    internal int RestoreOptions => _restoreOptions;

    internal BackupArchiveIdentity? ArchiveIdentity => _archiveBinding?.Identity ?? _archiveIdentity;

    internal BackupDataDirectoryIdentity? RawDataBackupIdentity { get; }

    internal void CleanupArchiveBinding() =>
        Interlocked.Exchange(ref _archiveBinding, null)?.Dispose();

    internal void EnsureAuthorizedForRestoreCommit() => EnsureAuthorized();

    ~Backup() => CleanupArchiveBinding();

    private bool HasContainsFlag(int flag)
    {
        EnsureAuthorized();
        return (_containsOptions & flag) != 0;
    }

    private bool HasRestoreFlag(int flag)
    {
        EnsureAuthorized();
        return (_restoreOptions & flag) != 0;
    }

    private void SetRestoreFlag(int flag, bool enabled)
    {
        EnsureAuthorized();
        _restoreOptions = enabled
            ? _restoreOptions | flag
            : _restoreOptions & ~flag;
    }

    private void EnsureAuthorized()
    {
        if (!_authorized || (_authorizationGuard is not null && !_authorizationGuard()))
        {
            throw new COMException(
                "Backup access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }
}
