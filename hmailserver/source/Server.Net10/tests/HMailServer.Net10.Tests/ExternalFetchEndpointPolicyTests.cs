using System.Net;
using HMailServer.Protocols.Pop3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ExternalFetchEndpointPolicyTests
{
    [TestMethod]
    public void SelectEndpoint_ReturnsFirstPublicAddressInResolverOrder()
    {
        var addresses = new[]
        {
            IPAddress.Parse("8.8.8.8"),
            IPAddress.Parse("1.1.1.1")
        };

        Assert.AreEqual(addresses[0], ExternalFetchEndpointPolicy.SelectEndpoint("pop3.example.test", addresses));
    }

    [TestMethod]
    public void SelectEndpoint_AllowsExplicitLocalhostWithOnlyLoopbackAnswers()
    {
        var addresses = new[]
        {
            IPAddress.Loopback,
            IPAddress.IPv6Loopback
        };

        var localhostDecision = ExternalFetchEndpointPolicy.Evaluate(
            "localhost",
            addresses,
            ["127.0.0.0/8", "::1/128"]);
        var literalDecision = ExternalFetchEndpointPolicy.Evaluate(
            "127.0.0.1",
            [IPAddress.Loopback],
            ["127.0.0.0/8"]);

        Assert.IsTrue(localhostDecision.IsAllowed);
        Assert.AreEqual(addresses[0], localhostDecision.Endpoint);
        Assert.IsTrue(literalDecision.IsAllowed);
        Assert.AreEqual(IPAddress.Loopback, literalDecision.Endpoint);
    }

    [TestMethod]
    public void Evaluate_DeniesExplicitLoopbackWithoutCidrAllowList()
    {
        var decision = ExternalFetchEndpointPolicy.Evaluate("localhost", [IPAddress.Loopback]);

        Assert.IsFalse(decision.IsAllowed);
        StringAssert.Contains(decision.Reason, "CIDR");
    }

    [TestMethod]
    public void SelectEndpoint_DeniesArbitraryHostnameResolvingToLoopback()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ExternalFetchEndpointPolicy.SelectEndpoint("pop3.example.test", [IPAddress.Loopback]));
    }

    [TestMethod]
    public void SelectEndpoint_DeniesMixedPublicAndSpecialUseAnswers()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ExternalFetchEndpointPolicy.SelectEndpoint(
                "pop3.example.test",
                [IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.1")]));
    }

    [TestMethod]
    public void Evaluate_AllowsPrivateAddressOnlyWithExplicitCidr()
    {
        var address = IPAddress.Parse("10.20.30.40");

        var denied = ExternalFetchEndpointPolicy.Evaluate("pop3.internal.test", [address]);
        var allowed = ExternalFetchEndpointPolicy.Evaluate(
            "pop3.internal.test",
            [address],
            ["10.0.0.0/8"]);

        Assert.IsFalse(denied.IsAllowed);
        Assert.IsTrue(allowed.IsAllowed);
        Assert.AreEqual(address, allowed.Endpoint);
    }

    [TestMethod]
    public void Evaluate_AuditModeCanObserveDeniedAddressWithoutChangingEndpointSelection()
    {
        var address = IPAddress.Parse("192.168.10.20");

        var decision = ExternalFetchEndpointPolicy.Evaluate("pop3.internal.test", [address]);

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(address, decision.Endpoint);
        StringAssert.Contains(decision.Reason, "special-use");
    }

    [TestMethod]
    public void SelectEndpoint_DeniesSpecialUseAddresses()
    {
        var addresses = new[]
        {
            "0.0.0.0",
            "10.0.0.1",
            "100.64.0.1",
            "169.254.169.254",
            "168.63.129.16",
            "172.16.0.1",
            "192.0.0.1",
            "192.0.2.1",
            "192.88.99.2",
            "192.168.1.1",
            "198.18.0.1",
            "198.51.100.1",
            "203.0.113.1",
            "224.0.0.1",
            "240.0.0.1",
            "255.255.255.255",
            "fc00::1",
            "fe80::1",
            "ff02::1",
            "64:ff9b::a9fe:a9fe",
            "64:ff9b:1::1",
            "100::1",
            "100:0:0:1::1",
            "2001:2::1",
            "2001:20::1",
            "2001:30::1",
            "2001:db8::1",
            "2002::1",
            "3fff::1",
            "5f00::1",
            "fd00:ec2::254",
            "::",
            "::ffff:10.0.0.1"
        };

        foreach (var address in addresses)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ExternalFetchEndpointPolicy.SelectEndpoint("pop3.example.test", [IPAddress.Parse(address)]));
        }
    }

    [TestMethod]
    public void SelectEndpoint_DeniesEmptyResolution()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ExternalFetchEndpointPolicy.SelectEndpoint("pop3.example.test", Array.Empty<IPAddress>()));
    }
}
