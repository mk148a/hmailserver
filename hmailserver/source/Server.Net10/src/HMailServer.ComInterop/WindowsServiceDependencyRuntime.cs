using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

public sealed class WindowsServiceDependencyRuntime : IServiceDependencyRuntime
{
    private const string RpcServiceName = "RPCSS";
    private const string ServerServiceName = "hMailServer";

    private readonly IWindowsServiceDependencyApi _api;

    public WindowsServiceDependencyRuntime()
        : this(new WindowsServiceDependencyApi())
    {
    }

    internal WindowsServiceDependencyRuntime(IWindowsServiceDependencyApi api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    public void MakeDependent(string otherService) =>
        _api.ReplaceDependencies(
            ServerServiceName,
            [RpcServiceName, otherService ?? string.Empty]);
}

internal interface IWindowsServiceDependencyApi
{
    void ReplaceDependencies(string serviceName, IReadOnlyList<string> dependencies);
}

internal sealed class WindowsServiceDependencyApi : IWindowsServiceDependencyApi
{
    private const uint ScManagerCreateService = 0x0002;
    private const uint ScManagerLock = 0x0008;
    private const uint ServiceChangeConfig = 0x0002;
    private const uint ServiceNoChange = 0xFFFFFFFF;

    public void ReplaceDependencies(
        string serviceName,
        IReadOnlyList<string> dependencies)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var serviceControlManager = OpenSCManager(
            machineName: null,
            databaseName: null,
            ScManagerCreateService | ScManagerLock);
        if (serviceControlManager == IntPtr.Zero)
        {
            return;
        }

        var service = IntPtr.Zero;
        var databaseLock = IntPtr.Zero;
        try
        {
            service = OpenService(
                serviceControlManager,
                serviceName,
                ServiceChangeConfig);
            if (service == IntPtr.Zero)
            {
                return;
            }

            databaseLock = LockServiceDatabase(serviceControlManager);
            if (databaseLock == IntPtr.Zero)
            {
                return;
            }

            var dependencyMultiString = Marshal.StringToHGlobalUni(
                BuildDependencyMultiString(dependencies));
            try
            {
                _ = ChangeServiceConfig(
                    service,
                    ServiceNoChange,
                    ServiceNoChange,
                    ServiceNoChange,
                    binaryPathName: null,
                    loadOrderGroup: null,
                    tagId: IntPtr.Zero,
                    dependencies: dependencyMultiString,
                    serviceStartName: null,
                    password: null,
                    displayName: null);
            }
            finally
            {
                Marshal.FreeHGlobal(dependencyMultiString);
            }
        }
        finally
        {
            if (databaseLock != IntPtr.Zero)
            {
                _ = UnlockServiceDatabase(databaseLock);
            }

            if (service != IntPtr.Zero)
            {
                _ = CloseServiceHandle(service);
            }

            _ = CloseServiceHandle(serviceControlManager);
        }
    }

    internal static string BuildDependencyMultiString(IReadOnlyList<string> dependencies) =>
        string.Join("\0", dependencies) + "\0\0";

    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr OpenSCManager(
        string? machineName,
        string? databaseName,
        uint desiredAccess);

    [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr OpenService(
        IntPtr serviceControlManager,
        string serviceName,
        uint desiredAccess);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr LockServiceDatabase(IntPtr serviceControlManager);

    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfigW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig(
        IntPtr service,
        uint serviceType,
        uint startType,
        uint errorControl,
        string? binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        IntPtr dependencies,
        string? serviceStartName,
        string? password,
        string? displayName);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnlockServiceDatabase(IntPtr databaseLock);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr serviceHandle);
}
