using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ComLocalServerHostTests
{
    private const uint CoinitMultithreaded = 0;
    private const uint ClsctxLocalServer = 0x4;
    private const int RpcEChangedMode = unchecked((int)0x80010106);
    private const int RegdbEClassNotRegistered = unchecked((int)0x80040154);

    [TestMethod]
    public void RegisteredFactory_ActivatesDirectMessageIndexingWithLegacyAccessDenied()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var initializeResult = CoInitializeEx(nint.Zero, CoinitMultithreaded);
        Assert.IsTrue(initializeResult >= 0 || initializeResult == RpcEChangedMode);

        var classId = Guid.NewGuid();
        using var host = new ComLocalServerHost(
            new ComLocalServerRegistration(classId, static () => new MessageIndexing()));

        try
        {
            host.Start();

            var interfaceId = typeof(IInterfaceMessageIndexing).GUID;
            var activateResult = CoCreateInstance(
                in classId,
                nint.Zero,
                ClsctxLocalServer,
                in interfaceId,
                out var interfacePointer);

            Assert.AreEqual(0, activateResult);
            Assert.AreNotEqual(nint.Zero, interfacePointer);

            try
            {
                var adapter = (IInterfaceMessageIndexing)Marshal.GetObjectForIUnknown(interfacePointer);
                var error = Assert.ThrowsExactly<COMException>(() => _ = adapter.TotalMessageCount);

                Assert.AreEqual(unchecked((int)0x80070005), error.ErrorCode);
                if (Marshal.IsComObject(adapter))
                {
                    Marshal.FinalReleaseComObject(adapter);
                }
            }
            finally
            {
                Marshal.Release(interfacePointer);
            }

            host.Dispose();

            var revokedInterfaceId = typeof(IInterfaceMessageIndexing).GUID;
            var revokedActivationResult = CoCreateInstance(
                in classId,
                nint.Zero,
                ClsctxLocalServer,
                in revokedInterfaceId,
                out var revokedInterfacePointer);

            Assert.AreEqual(RegdbEClassNotRegistered, revokedActivationResult);
            Assert.AreEqual(nint.Zero, revokedInterfacePointer);
        }
        finally
        {
            if (initializeResult >= 0)
            {
                CoUninitialize();
            }
        }
    }

    [TestMethod]
    public void RegisteredFactory_ActivatesApplicationAndAuthenticatesLegacyServerAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var initializeResult = CoInitializeEx(nint.Zero, CoinitMultithreaded);
        Assert.IsTrue(initializeResult >= 0 || initializeResult == RpcEChangedMode);

        var classId = Guid.NewGuid();
        using var host = new ComLocalServerHost(
            new ComLocalServerRegistration(
                classId,
                static () => new Application(new TestAdministratorAuthenticationProvider("secret"))));

        try
        {
            host.Start();

            var interfaceId = typeof(IInterfaceApplication).GUID;
            var activateResult = CoCreateInstance(
                in classId,
                nint.Zero,
                ClsctxLocalServer,
                in interfaceId,
                out var interfacePointer);

            Assert.AreEqual(0, activateResult);
            Assert.AreNotEqual(nint.Zero, interfacePointer);

            try
            {
                var application = (IInterfaceApplication)Marshal.GetObjectForIUnknown(interfacePointer);

                Assert.IsNull(application.Authenticate("Administrator", "wrong"));
                var account = application.Authenticate("administrator", "secret");
                Assert.IsNotNull(account);
                Assert.AreEqual(ComAdminLevel.ServerAdministrator, account.AdminLevel);

                if (Marshal.IsComObject(account))
                {
                    Marshal.FinalReleaseComObject(account);
                }

                if (Marshal.IsComObject(application))
                {
                    Marshal.FinalReleaseComObject(application);
                }
            }
            finally
            {
                Marshal.Release(interfacePointer);
            }
        }
        finally
        {
            if (initializeResult >= 0)
            {
                CoUninitialize();
            }
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(nint reserved, uint coInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        in Guid classId,
        nint outer,
        uint context,
        in Guid interfaceId,
        out nint interfacePointer);

    private sealed class TestAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            username.Equals("Administrator", StringComparison.OrdinalIgnoreCase)
            && attemptedPassword == password;
    }
}
