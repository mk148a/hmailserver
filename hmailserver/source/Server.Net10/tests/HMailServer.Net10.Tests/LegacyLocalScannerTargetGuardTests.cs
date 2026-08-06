using System.Net;
using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LegacyLocalScannerTargetGuardTests
{
    [TestMethod]
    public void IsLocalAddress_AcceptsLoopbackAndRejectsPublicAddresses()
    {
        Assert.IsTrue(LegacyLocalScannerTargetGuard.IsLocalAddress(IPAddress.Parse("127.0.0.1")));
        Assert.IsTrue(LegacyLocalScannerTargetGuard.IsLocalAddress(IPAddress.Parse("::1")));
        Assert.IsFalse(LegacyLocalScannerTargetGuard.IsLocalAddress(IPAddress.Parse("203.0.113.1")));
        Assert.IsFalse(LegacyLocalScannerTargetGuard.IsLocalAddress(IPAddress.Parse("8.8.8.8")));
    }

    [TestMethod]
    public void IsLocalTarget_AcceptsLocalLiteralAndRejectsPublicAddresses()
    {
        Assert.IsFalse(LegacyLocalScannerTargetGuard.IsLocalTarget(string.Empty));
        Assert.IsFalse(LegacyLocalScannerTargetGuard.IsLocalTarget(null!));
        Assert.IsTrue(LegacyLocalScannerTargetGuard.IsLocalTarget("127.0.0.1"));
        Assert.IsFalse(LegacyLocalScannerTargetGuard.IsLocalTarget("203.0.113.1"));
    }
}