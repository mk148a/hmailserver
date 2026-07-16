using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

// These identifiers are additive and intentionally remain outside the legacy
// registration manifest until the caller-access and AppID preflight gates pass.
[ComVisible(false)]
public static class WebAdminSessionBrokerContract
{
    public const string TypeLibraryId = "762389D5-F39C-4C1E-85C0-226A04F51549";
    public const string InterfaceId = "30B665E9-0355-47A0-8D43-2D3500CBF202";
    public const string ClassId = "0075EA16-D379-4E60-864F-345C6196873B";
    public const string AppId = "FBF541A0-4A8F-413C-9064-0938C46E26E4";
    public const string VersionedProgId = "hMailServer.WebAdminSessionBroker.1";
    public const string VersionIndependentProgId = "hMailServer.WebAdminSessionBroker";
}

[ComVisible(true)]
[Guid(WebAdminSessionBrokerContract.InterfaceId)]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceWebAdminSessionBroker
{
    [DispId(1)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string CreateSession(
        [MarshalAs(UnmanagedType.BStr)] string username,
        [MarshalAs(UnmanagedType.BStr)] string password,
        [MarshalAs(UnmanagedType.BStr)] string phpSessionId);

    [DispId(2)]
    IInterfaceApplication OpenApplication(
        [MarshalAs(UnmanagedType.BStr)] string rawToken,
        [MarshalAs(UnmanagedType.BStr)] string phpSessionId);

    [DispId(3)]
    void Revoke(
        [MarshalAs(UnmanagedType.BStr)] string rawToken,
        [MarshalAs(UnmanagedType.BStr)] string phpSessionId);

    [DispId(4)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string RotateAfterOwnPasswordChange(
        [MarshalAs(UnmanagedType.BStr)] string rawToken,
        [MarshalAs(UnmanagedType.BStr)] string phpSessionId,
        [MarshalAs(UnmanagedType.BStr)] string newPassword);
}
