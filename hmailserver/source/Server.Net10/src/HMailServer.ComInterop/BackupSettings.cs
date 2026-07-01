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
    private const int BackupSettingsFlag = 1;
    private const int BackupDomainsFlag = 2;
    private const int BackupMessagesFlag = 4;
    private const int CompressDestinationFilesFlag = 8;

    private readonly BackupSettingsAdministrationSnapshot? _snapshot;

    public BackupSettings()
    {
    }

    private BackupSettings(BackupSettingsAdministrationSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public override string Destination { get => Snapshot.Destination; set => base.Destination = value; }

    protected override bool GetBackupSettings() => HasFlag(BackupSettingsFlag);

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

    internal static BackupSettings CreateAuthorized(BackupSettingsAdministrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new BackupSettings(snapshot);
    }

    private BackupSettingsAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "BackupSettings access requires an authenticated server administrator.",
            EAccessDenied);

    private bool HasFlag(int flag) => (Snapshot.Options & flag) != 0;
}

[ComVisible(false)]
public abstract class BackupSettingsComAdapter : IInterfaceBackupSettings
{
    public virtual string Destination { get => Unavailable<string>(); set => Unavailable(); }
    public bool BackupSettings { get => GetBackupSettings(); set => Unavailable(); }
    public virtual bool BackupDomains { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool BackupMessages { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool CompressDestinationFiles { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string LogFile => Unavailable<string>();

    protected virtual bool GetBackupSettings() => Unavailable<bool>();

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
