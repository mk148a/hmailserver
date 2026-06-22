using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceApplication
{
    [DispId(1)]
    void Start();

    [DispId(2)]
    void Stop();

    [DispId(3)]
    IInterfaceSettings Settings { get; }

    [DispId(4)]
    IInterfaceDomains Domains { get; }

    [DispId(5)]
    ComServerState ServerState { get; }

    [DispId(6)]
    IInterfaceDatabase Database { get; }

    [DispId(7)]
    IInterfaceUtilities Utilities { get; }

    [DispId(8)]
    void SubmitEMail();

    [DispId(9)]
    IInterfaceStatus Status { get; }

    [DispId(10)]
    string Version { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(11)]
    void Connect();

    [DispId(12)]
    string InitializationFile { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(13)]
    void Reinitialize();

    [DispId(14)]
    IInterfaceRules Rules { get; }

    [DispId(15)]
    IInterfaceBackupManager BackupManager { get; }

    [DispId(16)]
    IInterfaceGlobalObjects GlobalObjects { get; }

    [DispId(17)]
    IInterfaceAccount? Authenticate(
        [MarshalAs(UnmanagedType.BStr)] string username,
        [MarshalAs(UnmanagedType.BStr)] string password);

    [DispId(18)]
    IInterfaceLinks Links { get; }

    [DispId(19)]
    IInterfaceDiagnostics Diagnostics { get; }

    [DispId(20)]
    string VersionArchitecture { [return: MarshalAs(UnmanagedType.BStr)] get; }
}
