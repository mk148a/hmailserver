using System.Net;
using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RemoteSmtpLocalEndpointPolicyTests
{
    [TestMethod]
    public void EnsureAllowed_RejectsLoopbackListeningPort()
    {
        var endpoint = CreateEndpoint(IPAddress.Loopback, 2525);
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.Loopback, 2525)]);

        Assert.ThrowsExactly<RemoteSmtpLocalEndpointDeniedException>(() => policy.EnsureAllowed(endpoint));
    }

    [TestMethod]
    public void EnsureAllowed_AllowsLoopbackUnusedPort()
    {
        var endpoint = CreateEndpoint(IPAddress.Loopback, 2526);
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.Loopback, 2525)]);

        policy.EnsureAllowed(endpoint);
    }

    [TestMethod]
    public void EnsureAllowed_RejectsWildcardListenerForLocalAddress()
    {
        var endpoint = CreateEndpoint(IPAddress.Loopback, 2525);
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.Any, 2525)],
            () => [IPAddress.Loopback]);

        Assert.ThrowsExactly<RemoteSmtpLocalEndpointDeniedException>(() => policy.EnsureAllowed(endpoint));
    }

    [TestMethod]
    public void EnsureAllowed_AllowsWildcardListenerForNonLocalAddress()
    {
        var endpoint = CreateEndpoint(IPAddress.Parse("192.0.2.10"), 2525);
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.Any, 2525)],
            () => [IPAddress.Loopback]);

        policy.EnsureAllowed(endpoint);
    }

    [TestMethod]
    public void EnsureAllowed_RejectsIpv6ListeningPort()
    {
        var endpoint = CreateEndpoint(IPAddress.IPv6Loopback, 2525);
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.IPv6Any, 2525)],
            () => [IPAddress.IPv6Loopback]);

        Assert.ThrowsExactly<RemoteSmtpLocalEndpointDeniedException>(() => policy.EnsureAllowed(endpoint));
    }

    [TestMethod]
    public void EnsureAllowed_NormalizesIpv4MappedIpv6Address()
    {
        var endpoint = CreateEndpoint(IPAddress.Parse("::ffff:127.0.0.1"), 2525);
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.Loopback, 2525)]);

        Assert.ThrowsExactly<RemoteSmtpLocalEndpointDeniedException>(() => policy.EnsureAllowed(endpoint));
    }

    [TestMethod]
    public void EnsureAllowed_RejectsIpv4MappedLoopbackForIpv6WildcardListener()
    {
        var endpoint = CreateEndpoint(IPAddress.Parse("::ffff:127.0.0.1"), 2525);
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.IPv6Any, 2525)]);

        Assert.ThrowsExactly<RemoteSmtpLocalEndpointDeniedException>(() => policy.EnsureAllowed(endpoint));
    }

    [TestMethod]
    public void EnsureAllowed_DoesNotInspectUnmarkedRouteEndpoint()
    {
        var endpoint = new RemoteSmtpEndpoint(
            "localhost",
            2525,
            RemoteSmtpConnectionSecurity.None,
            ConnectionAddress: IPAddress.Loopback.ToString());
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.Loopback, 2525)]);

        policy.EnsureAllowed(endpoint);
    }

    private static RemoteSmtpEndpoint CreateEndpoint(IPAddress address, int port) =>
        new(
            "dns-derived.example",
            port,
            RemoteSmtpConnectionSecurity.None,
            ConnectionAddress: address.ToString(),
            EnforceLocalEndpointGuard: true);
}
