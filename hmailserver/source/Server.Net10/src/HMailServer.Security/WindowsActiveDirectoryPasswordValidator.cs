using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class WindowsActiveDirectoryPasswordValidator : IActiveDirectoryPasswordValidator
{
    private readonly Func<string, string, string, bool> _validate;

    public WindowsActiveDirectoryPasswordValidator(
        Func<string, string, string, bool>? validate = null)
    {
        _validate = validate ?? ValidateWindowsUser;
    }

    public bool Validate(string domain, string username, string password)
    {
        if (string.IsNullOrEmpty(domain)
            || string.IsNullOrEmpty(username)
            || string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            return _validate(domain, username, password);
        }
        catch
        {
            return false;
        }
    }

    private static bool ValidateWindowsUser(
        string domain,
        string username,
        string password)
    {
        var token = IntPtr.Zero;
        try
        {
            return LogonUser(
                username,
                domain,
                password,
                Logon32LogonNetwork,
                Logon32ProviderDefault,
                out token);
        }
        finally
        {
            if (token != IntPtr.Zero)
            {
                CloseHandle(token);
            }
        }
    }

    private const int Logon32LogonNetwork = 3;
    private const int Logon32ProviderDefault = 0;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUser(
        string username,
        string domain,
        string password,
        int logonType,
        int logonProvider,
        out IntPtr token);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
