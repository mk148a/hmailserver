using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("2C5559F0-DF3F-43C0-935C-F79D41CF8A5B")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceBackupSettings
{
    [DispId(1)]
    string Destination
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(2)]
    bool BackupSettings
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(3)]
    bool BackupDomains
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(4)]
    bool BackupMessages
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(5)]
    bool CompressDestinationFiles
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(7)]
    string LogFile { [return: MarshalAs(UnmanagedType.BStr)] get; }
}

[ComVisible(true)]
[Guid("E0213ECF-BAEC-4E20-9813-0F75A97D0B16")]
[ProgId("hMailServer.BackupSettings.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceBackupSettings))]
public sealed class BackupSettings : BackupSettingsComAdapter
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int BackupSettingsFlag = 1;
    private const int BackupDomainsFlag = 2;
    private const int BackupMessagesFlag = 4;
    private const int CompressDestinationFilesFlag = 8;

    private BackupSettingsAdministrationSnapshot? _snapshot;
    private readonly Func<string, bool>? _updateDestination;
    private readonly Action<string>? _destinationUpdated;
    private readonly Func<bool, bool>? _updateBackupSettings;
    private readonly Action<int>? _backupSettingsUpdated;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly Func<CancellationToken, ValueTask<IDisposable?>>? _authorizationLeaseFactory;

    public BackupSettings()
    {
    }

    private BackupSettings(
        BackupSettingsAdministrationSnapshot snapshot,
        Func<string, bool>? updateDestination,
        Action<string>? destinationUpdated,
        Func<bool, bool>? updateBackupSettings,
        Action<int>? backupSettingsUpdated,
        Func<bool>? isServerAdministrator,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory)
    {
        _snapshot = snapshot;
        _updateDestination = updateDestination;
        _destinationUpdated = destinationUpdated;
        _updateBackupSettings = updateBackupSettings;
        _backupSettingsUpdated = backupSettingsUpdated;
        _isServerAdministrator = isServerAdministrator;
        _authorizationLeaseFactory = authorizationLeaseFactory;
    }

    public override string Destination
    {
        get => Snapshot.Destination;
        set
        {
            var snapshot = Snapshot;
            if (_updateDestination is null)
            {
                base.Destination = value;
                return;
            }

            EnsureServerAdministrator();
            using var authorizationLease = AcquireAuthorizationLease();
            if (!_updateDestination(value))
            {
                throw new COMException(
                    "The backup destination update did not affect the existing settings row.",
                    EFail);
            }

            _snapshot = snapshot with { Destination = value };
            _destinationUpdated?.Invoke(value);
        }
    }

    protected override bool GetBackupSettings() => HasFlag(BackupSettingsFlag);

    protected override void SetBackupSettings(bool value)
    {
        var snapshot = Snapshot;
        if (_updateBackupSettings is null)
        {
            base.SetBackupSettings(value);
            return;
        }

        EnsureServerAdministrator();
        using var authorizationLease = AcquireAuthorizationLease();
        if (!_updateBackupSettings(value))
        {
            throw new COMException(
                "The backup settings update did not affect the existing settings row.",
                EFail);
        }

        var options = value
            ? snapshot.Options | BackupSettingsFlag
            : snapshot.Options & ~BackupSettingsFlag;
        _snapshot = snapshot with { Options = options };
        _backupSettingsUpdated?.Invoke(options);
    }

    public override bool BackupDomains
    {
        get => HasFlag(BackupDomainsFlag);
        set => base.BackupDomains = value;
    }

    public override bool BackupMessages
    {
        get => HasFlag(BackupMessagesFlag);
        set => base.BackupMessages = value;
    }

    public override bool CompressDestinationFiles
    {
        get => HasFlag(CompressDestinationFilesFlag);
        set => base.CompressDestinationFiles = value;
    }

    public override string LogFile
    {
        get
        {
            var directory = Snapshot.LogDirectory;
            if (!directory.EndsWith('\\'))
            {
                directory += "\\";
            }

            return directory + "hmailserver_backup.log";
        }
    }

    internal static BackupSettings CreateAuthorized(
        BackupSettingsAdministrationSnapshot snapshot,
        Func<string, bool>? updateDestination = null,
        Action<string>? destinationUpdated = null,
        Func<bool, bool>? updateBackupSettings = null,
        Action<int>? backupSettingsUpdated = null,
        Func<bool>? isServerAdministrator = null,
        Func<CancellationToken, ValueTask<IDisposable?>>? authorizationLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new BackupSettings(
            snapshot,
            updateDestination,
            destinationUpdated,
            updateBackupSettings,
            backupSettingsUpdated,
            isServerAdministrator,
            authorizationLeaseFactory);
    }

    private BackupSettingsAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "BackupSettings access requires an authenticated server administrator.",
            EAccessDenied);

    private bool HasFlag(int flag) => (Snapshot.Options & flag) != 0;

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "Settings access requires an authenticated server administrator.",
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
                "Settings access requires an authenticated server administrator.",
                EAccessDenied);
    }
}

[ComVisible(false)]
public abstract class BackupSettingsComAdapter : IInterfaceBackupSettings
{
    public virtual string Destination { get => Unavailable<string>(); set => Unavailable(); }
    public bool BackupSettings { get => GetBackupSettings(); set => SetBackupSettings(value); }
    public virtual bool BackupDomains { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool BackupMessages { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool CompressDestinationFiles { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string LogFile => Unavailable<string>();

    protected virtual bool GetBackupSettings() => Unavailable<bool>();

    protected virtual void SetBackupSettings(bool value) => Unavailable();

    private T Unavailable<T>() => BackupSettingsComAuthorization.Unavailable<T>(this);

    private void Unavailable() => BackupSettingsComAuthorization.Unavailable(this);
}

[ComVisible(false)]
internal static class BackupSettingsComAuthorization
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    internal static T Unavailable<T>(IInterfaceBackupSettings backupSettings)
    {
        EnsureAuthorized(backupSettings);
        throw new COMException(
            "This BackupSettings member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    internal static void Unavailable(IInterfaceBackupSettings backupSettings)
    {
        EnsureAuthorized(backupSettings);
        throw new COMException(
            "This BackupSettings member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private static void EnsureAuthorized(IInterfaceBackupSettings backupSettings)
    {
        if (backupSettings is BackupSettings authorized)
        {
            _ = authorized.Destination;
            return;
        }

        throw new COMException(
            "BackupSettings access requires an authenticated server administrator.",
            EAccessDenied);
    }
}
