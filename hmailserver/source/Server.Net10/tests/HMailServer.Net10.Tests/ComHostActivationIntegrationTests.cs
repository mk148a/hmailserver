using System.Runtime.InteropServices;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ComHostActivationIntegrationTests
{
    private static readonly Guid RoutesClsid = new("7D174A9D-D44C-4627-BE78-E5DDC513C31F");
    private static readonly Guid IClassFactoryGuid = new("00000001-0000-0000-C000-000000000046");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DllGetClassObjectDelegate(
        ref Guid rclsid,
        ref Guid riid,
        out IntPtr ppv);

    [TestMethod]
    public void ComHost_ExportsDllGetClassObjectAndRecordsHostRuntimeDependency()
    {
        var module = IntPtr.Zero;
        try
        {
            var comhostPath = LocateComHost();
            module = LoadLibrary(comhostPath);
            Assert.AreNotEqual(IntPtr.Zero, module, "Failed to load HMailServer.ComInterop.comhost.dll.");

            var proc = GetProcAddress(module, "DllGetClassObject");
            Assert.AreNotEqual(IntPtr.Zero, proc, "DllGetClassObject export was not found.");

            var dllGetClassObject = Marshal.GetDelegateForFunctionPointer<DllGetClassObjectDelegate>(proc);
            var clsId = RoutesClsid;
            var classFactoryId = IClassFactoryGuid;
            var hr = dllGetClassObject(ref clsId, ref classFactoryId, out _);

            // The .NET comhost cannot bootstrap its runtime from inside another .NET
            // process; genuine out-of-proc activation requires the service host/runtime
            // registration (registry/DCOM state), which remains fenced.
            Assert.AreEqual(unchecked((int)0x80008093), hr);
        }
        finally
        {
            if (module != IntPtr.Zero)
            {
                FreeLibrary(module);
            }
        }
    }

    private static string LocateComHost()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "HMailServer.ComInterop.comhost.dll");
            if (File.Exists(path))
            {
                return path;
            }
        }

        Assert.Fail("Could not locate HMailServer.ComInterop.comhost.dll next to the test host.");
        return string.Empty;
    }
}