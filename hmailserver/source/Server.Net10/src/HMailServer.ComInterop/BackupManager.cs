using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("E773E8FC-1C9A-4E96-A73C-CC02E7649637")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceBackupManager
{
    [DispId(1)]
    void StartBackup();

    [DispId(2)]
    IInterfaceBackup LoadBackup([MarshalAs(UnmanagedType.BStr)] string xmlFile);
}

[ComVisible(true)]
[Guid("1BBE5234-D331-41DF-85D7-CAF0B00B3BF7")]
[ProgId("hMailServer.BackupManager.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceBackupManager))]
public sealed class BackupManager : IInterfaceBackupManager
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly bool _authorized;

    public BackupManager()
    {
    }

    private BackupManager(bool authorized)
    {
        _authorized = authorized;
    }

    public void StartBackup()
    {
        EnsureAuthorized();
        throw NotImplemented();
    }

    public IInterfaceBackup LoadBackup(string xmlFile)
    {
        EnsureAuthorized();
        throw NotImplemented();
    }

    internal static BackupManager CreateAuthorized() => new(authorized: true);

    private void EnsureAuthorized()
    {
        if (!_authorized)
        {
            throw new COMException(
                "BackupManager access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private static COMException NotImplemented() => new(
        "This BackupManager member is not implemented by the .NET 10 rewrite yet.",
        ENotImplemented);
}
