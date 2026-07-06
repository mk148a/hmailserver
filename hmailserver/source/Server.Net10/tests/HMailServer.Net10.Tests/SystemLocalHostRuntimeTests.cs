using System.Net;
using System.Net.Sockets;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SystemLocalHostRuntimeTests
{
    [TestMethod]
    public void IsLocalHost_LiteralIPv4ComparesDirectlyWithoutDnsLookup()
    {
        var resolver = new RecordingAddressResolver([]);
        var runtime = new SystemLocalHostRuntime(
            resolver,
            new FixedLocalIpAddressProvider([IPAddress.Parse("192.0.2.10")]));

        Assert.IsTrue(runtime.IsLocalHost("192.0.2.10"));
        Assert.IsFalse(runtime.IsLocalHost("192.0.2.11"));
        Assert.AreEqual(0, resolver.CallCount);
    }

    [TestMethod]
    public void IsLocalHost_HostnameUsesOnlyFirstResolvedIPv4Address()
    {
        var firstResolvedIpv4 = IPAddress.Parse("192.0.2.20");
        var laterLocalIpv4 = IPAddress.Parse("192.0.2.21");
        var resolver = new RecordingAddressResolver(
            [IPAddress.Parse("2001:db8::1"), firstResolvedIpv4, laterLocalIpv4]);
        var runtime = new SystemLocalHostRuntime(
            resolver,
            new FixedLocalIpAddressProvider([laterLocalIpv4]));

        Assert.IsFalse(runtime.IsLocalHost("mail.example.test"));
        Assert.AreEqual("mail.example.test", resolver.LastHostName);
        Assert.AreEqual(1, resolver.CallCount);
    }

    [TestMethod]
    public void IsLocalHost_HostnameReturnsTrueWhenFirstResolvedIPv4IsLocal()
    {
        var localAddress = IPAddress.Parse("198.51.100.25");
        var runtime = new SystemLocalHostRuntime(
            new RecordingAddressResolver([IPAddress.Parse("2001:db8::2"), localAddress]),
            new FixedLocalIpAddressProvider([localAddress]));

        Assert.IsTrue(runtime.IsLocalHost("local.example.test"));
    }

    [TestMethod]
    public void IsLocalHost_UnresolvedIpv6OnlyAndEmptyInputsReturnFalse()
    {
        var localAddress = IPAddress.Parse("203.0.113.30");
        var localAddresses = new FixedLocalIpAddressProvider([localAddress]);

        Assert.IsFalse(
            new SystemLocalHostRuntime(
                new ThrowingAddressResolver(),
                localAddresses)
                .IsLocalHost("missing.example.test"));
        Assert.IsFalse(
            new SystemLocalHostRuntime(
                new RecordingAddressResolver([IPAddress.Parse("2001:db8::3")]),
                localAddresses)
                .IsLocalHost("ipv6-only.example.test"));
        Assert.IsFalse(
            new SystemLocalHostRuntime(
                new RecordingAddressResolver([localAddress]),
                localAddresses)
                .IsLocalHost("::1"));
        Assert.IsFalse(
            new SystemLocalHostRuntime(
                new RecordingAddressResolver([localAddress]),
                localAddresses)
                .IsLocalHost(string.Empty));
    }

    private sealed class RecordingAddressResolver(IReadOnlyList<IPAddress> addresses)
        : IDnsAddressResolver
    {
        public int CallCount { get; private set; }

        public string? LastHostName { get; private set; }

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
            string hostName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastHostName = hostName;
            return ValueTask.FromResult(addresses);
        }
    }

    private sealed class ThrowingAddressResolver : IDnsAddressResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
            string hostName,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<IReadOnlyList<IPAddress>>(
                new SocketException((int)SocketError.HostNotFound));
    }

    private sealed class FixedLocalIpAddressProvider(IReadOnlyList<IPAddress> addresses)
        : ILocalIpAddressProvider
    {
        public IReadOnlyList<IPAddress> GetLocalIPv4Addresses() => addresses;
    }
}
