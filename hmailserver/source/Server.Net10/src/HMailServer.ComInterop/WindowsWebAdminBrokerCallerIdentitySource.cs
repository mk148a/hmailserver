using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace HMailServer.ComInterop;

// This seam keeps the COM token capture testable without creating a COM call
// or changing the installed hMailServer registration during unit tests.
[ComVisible(false)]
public interface IWebAdminBrokerCallerIdentityNative
{
    bool TryCaptureCaller(out WebAdminBrokerCallerIdentity identity);

    bool RevertToSelf();
}

[ComVisible(false)]
[SupportedOSPlatform("windows")]
public sealed class WindowsWebAdminBrokerCallerIdentitySource : IWebAdminBrokerCallerIdentitySource
{
    private readonly IWebAdminBrokerCallerIdentityNative _native;

    public WindowsWebAdminBrokerCallerIdentitySource()
        : this(new WindowsWebAdminBrokerCallerIdentityNative())
    {
    }

    public WindowsWebAdminBrokerCallerIdentitySource(IWebAdminBrokerCallerIdentityNative native)
    {
        ArgumentNullException.ThrowIfNull(native);
        _native = native;
    }

    public WebAdminBrokerCallerIdentity? CaptureImpersonatedCaller() =>
        _native.TryCaptureCaller(out var identity) ? identity : null;

    public bool RevertToSelf() => _native.RevertToSelf();
}

[ComVisible(false)]
[SupportedOSPlatform("windows")]
internal sealed class WindowsWebAdminBrokerCallerIdentityNative : IWebAdminBrokerCallerIdentityNative
{
    private const uint TokenQuery = 0x0008;
    private const int ErrorInsufficientBuffer = 122;
    private const int TokenUserInformation = 1;
    private const int TokenType = 8;
    private const int TokenImpersonationLevel = 9;

    public bool TryCaptureCaller(out WebAdminBrokerCallerIdentity identity)
    {
        identity = default!;

        if (!OperatingSystem.IsWindows() || CoImpersonateClient() < 0)
        {
            return false;
        }

        if (!OpenThreadToken(GetCurrentThread(), TokenQuery, true, out var token))
        {
            return false;
        }

        try
        {
            if (!TryReadTokenInt32(token, TokenType, out var tokenType)
                || !TryReadTokenInt32(token, TokenImpersonationLevel, out var impersonationLevel)
                || !TryReadTokenSid(token, out var sid))
            {
                return false;
            }

            // A remote caller cannot own the local IIS virtual-account SID.
            // The guard still performs the authoritative normalized SID match;
            // this source deliberately does not infer transport from a bearer token.
            identity = new WebAdminBrokerCallerIdentity(
                sid,
                (WebAdminBrokerTokenType)tokenType,
                (WebAdminBrokerImpersonationLevel)impersonationLevel,
                IsRemote: false);
            return true;
        }
        finally
        {
            _ = CloseHandle(token);
        }
    }

    public bool RevertToSelf() =>
        OperatingSystem.IsWindows() && CoRevertToSelf() >= 0;

    private static bool TryReadTokenInt32(nint token, int informationClass, out int value)
    {
        value = 0;
        _ = GetTokenInformation(token, informationClass, nint.Zero, 0, out var length);
        if (length == 0 || Marshal.GetLastWin32Error() != ErrorInsufficientBuffer)
        {
            return false;
        }

        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (!GetTokenInformation(token, informationClass, buffer, length, out _))
            {
                return false;
            }

            value = Marshal.ReadInt32(buffer);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool TryReadTokenSid(nint token, out string sid)
    {
        sid = string.Empty;
        _ = GetTokenInformation(token, TokenUserInformation, nint.Zero, 0, out var length);
        if (length == 0 || Marshal.GetLastWin32Error() != ErrorInsufficientBuffer)
        {
            return false;
        }

        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (!GetTokenInformation(token, TokenUserInformation, buffer, length, out _))
            {
                return false;
            }

            var tokenUser = Marshal.PtrToStructure<NativeTokenUser>(buffer);
            sid = new SecurityIdentifier(tokenUser.User.Sid).Value;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CoImpersonateClient();

    [DllImport("ole32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CoRevertToSelf();

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetCurrentThread();

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenThreadToken(
        nint threadHandle,
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool openAsSelf,
        out nint tokenHandle);

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        nint tokenHandle,
        int tokenInformationClass,
        nint tokenInformation,
        uint tokenInformationLength,
        out uint returnLength);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeTokenUser
    {
        public SidAndAttributes User;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes
    {
        public nint Sid;
        public uint Attributes;
    }
}
