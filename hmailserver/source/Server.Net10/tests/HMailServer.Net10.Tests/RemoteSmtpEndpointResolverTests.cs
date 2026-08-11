using System.Net;
using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RemoteSmtpEndpointResolverTests
{
    [TestMethod]
    public async Task ResolveAsync_UsesConfiguredRouteTargetAndPreservesSettings()
    {
        var mxResolver = new FakeMxResolver();
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["relay.customer.example"] = [IPAddress.Parse("192.0.2.10")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "route:5",
            "customer.example",
            Route: new SmtpRouteResolution(
                RouteId: 5,
                DomainName: "*.customer.example",
                TargetHost: "relay.customer.example",
                TargetPort: 2525,
                ConnectionSecurity: (int)RemoteSmtpConnectionSecurity.StartTlsRequired,
                TreatRecipientAsLocal: false,
                RequiresAuthentication: true,
                AuthenticationUsername: "relay-user",
                AuthenticationPassword: "relay-secret"));

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual("relay.customer.example", endpoint.Host);
        Assert.AreEqual(2525, endpoint.Port);
        Assert.AreEqual(RemoteSmtpConnectionSecurity.StartTlsRequired, endpoint.ConnectionSecurity);
        Assert.IsTrue(endpoint.RequiresAuthentication);
        Assert.AreEqual("relay-user", endpoint.AuthenticationUsername);
        Assert.AreEqual("relay-secret", endpoint.AuthenticationPassword);
        Assert.IsTrue(endpoint.EnforceLocalEndpointGuard);
        Assert.AreEqual("192.0.2.10", endpoint.ConnectionAddress);
        Assert.AreEqual(0, mxResolver.CallCount);
    }

    [TestMethod]
    public async Task ResolveAsync_ExpandsConfiguredRouteHostsInOrderDeduplicatesAndCapsAfterFlattening()
    {
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["first.example"] = [IPAddress.Parse("192.0.2.1"), IPAddress.Parse("192.0.2.2")],
            ["second.example"] = [IPAddress.Parse("192.0.2.2"), IPAddress.Parse("192.0.2.3"), IPAddress.Parse("192.0.2.4")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "route:5",
            "customer.example",
            Route: new SmtpRouteResolution(
                5,
                "*.customer.example",
                " first.example | second.example ",
                2525,
                (int)RemoteSmtpConnectionSecurity.StartTlsRequired,
                false),
            MaxNumberOfMxHosts: 3);

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "first.example", "first.example", "second.example" },
            endpoint.GetCandidates().Select(static candidate => candidate.Host).ToArray());
        CollectionAssert.AreEqual(
            new[] { "192.0.2.1", "192.0.2.2", "192.0.2.3" },
            endpoint.GetCandidates().Select(static candidate => candidate.ConnectionAddress).ToArray());
        CollectionAssert.AreEqual(
            new[] { "first.example", "second.example" },
            addressResolver.RequestedHosts.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_ConfiguredRouteRetainsSuccessfulHostsAfterPartialResolutionFailure()
    {
        var addressResolver = new FakeAddressResolver(
            new Dictionary<string, IReadOnlyList<IPAddress>>
            {
                ["good.example"] = [IPAddress.Parse("192.0.2.20")]
            },
            new Dictionary<string, Exception>
            {
                ["bad.example"] = new IOException("DNS failure")
            });
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "route:5",
            "customer.example",
            Route: new SmtpRouteResolution(
                5,
                "*.customer.example",
                "bad.example|good.example",
                2525,
                (int)RemoteSmtpConnectionSecurity.None,
                false));

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual("good.example", endpoint.Host);
        Assert.AreEqual("192.0.2.20", endpoint.ConnectionAddress);
        Assert.IsTrue(endpoint.EnforceLocalEndpointGuard);
    }

    [TestMethod]
    public async Task ResolveAsync_ConfiguredRouteLiteralBypassesDnsAndUsesLiteralAddress()
    {
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>());
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "route:5",
            "customer.example",
            Route: new SmtpRouteResolution(
                5,
                "*.customer.example",
                "192.0.2.30",
                2525,
                (int)RemoteSmtpConnectionSecurity.None,
                false));

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual("192.0.2.30", endpoint.Host);
        Assert.AreEqual("192.0.2.30", endpoint.ConnectionAddress);
        Assert.IsTrue(endpoint.EnforceLocalEndpointGuard);
        Assert.AreEqual(0, addressResolver.RequestedHosts.Count);
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsLiteralConfiguredRouteActiveListener()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "route:5",
            "customer.example",
            Route: new SmtpRouteResolution(
                5,
                "*.customer.example",
                IPAddress.Loopback.ToString(),
                2525,
                (int)RemoteSmtpConnectionSecurity.None,
                false));

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.Loopback, 2525)]);

        Assert.IsTrue(endpoint.EnforceLocalEndpointGuard);
        Assert.AreEqual(IPAddress.Loopback.ToString(), endpoint.ConnectionAddress);
        Assert.ThrowsExactly<RemoteSmtpLocalEndpointDeniedException>(() => policy.EnsureAllowed(endpoint));
    }

    [TestMethod]
    public async Task ResolveAsync_AllowsLiteralConfiguredRouteUnusedLoopbackPort()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "route:5",
            "customer.example",
            Route: new SmtpRouteResolution(
                5,
                "*.customer.example",
                IPAddress.Loopback.ToString(),
                2526,
                (int)RemoteSmtpConnectionSecurity.None,
                false));

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.Loopback, 2525)]);

        policy.EnsureAllowed(endpoint);
    }

    [TestMethod]
    public async Task ResolveAsync_UsesRelayerTargetWithoutMx_AndDefaultsPort()
    {
        var mxResolver = new FakeMxResolver();
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
            CreateAddressResolver("relay.example"),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "relayer:relay.example",
            "one.example",
            Route: new SmtpRouteResolution(
                RouteId: 0,
                DomainName: "*",
                TargetHost: "relay.example",
                TargetPort: 0,
                ConnectionSecurity: (int)RemoteSmtpConnectionSecurity.None,
                TreatRecipientAsLocal: false));

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual("relay.example", endpoint.Host);
        Assert.AreEqual(25, endpoint.Port);
        Assert.IsFalse(endpoint.RequiresAuthentication);
        Assert.IsTrue(endpoint.GetCandidates().Single().EnforceLocalEndpointGuard);
        Assert.AreEqual(0, mxResolver.CallCount);
    }

    [TestMethod]
    public async Task ResolveAsync_UsesAuthenticatedRelayerCredentials()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            CreateAddressResolver("relay.example"),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "relayer:relay.example",
            "one.example",
            Route: new SmtpRouteResolution(
                0,
                "*",
                "relay.example",
                2525,
                (int)RemoteSmtpConnectionSecurity.StartTlsRequired,
                false,
                true,
                "relay-user",
                "relay-secret"));

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.IsTrue(endpoint.RequiresAuthentication);
        Assert.AreEqual("relay-user", endpoint.AuthenticationUsername);
        Assert.AreEqual("relay-secret", endpoint.AuthenticationPassword);
        Assert.AreEqual(RemoteSmtpConnectionSecurity.StartTlsRequired, endpoint.ConnectionSecurity);
    }

    [TestMethod]
    public async Task ResolveAsync_ExpandsGlobalRelayerHostsInOrderAndPropagatesSettings()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
            {
                ["first.example"] = [IPAddress.Parse("192.0.2.1")],
                ["second.example"] = [IPAddress.Parse("192.0.2.2")]
            }),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "relayer:first.example",
            "one.example",
            Route: new SmtpRouteResolution(
                0,
                "*",
                " first.example || second.example | ",
                587,
                (int)RemoteSmtpConnectionSecurity.StartTlsRequired,
                false,
                true,
                "relay-user",
                "relay-secret"));

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);
        var candidates = endpoint.GetCandidates();

        CollectionAssert.AreEqual(
            new[] { "first.example", "second.example" },
            candidates.Select(static candidate => candidate.Host).ToArray());
        Assert.IsTrue(candidates.All(static candidate => candidate.Port == 587));
        Assert.IsTrue(candidates.All(static candidate => candidate.ConnectionSecurity == RemoteSmtpConnectionSecurity.StartTlsRequired));
        Assert.IsTrue(candidates.All(static candidate => candidate.RequiresAuthentication));
        Assert.IsTrue(candidates.All(static candidate => candidate.AuthenticationUsername == "relay-user"));
        Assert.IsTrue(candidates.All(static candidate => candidate.AuthenticationPassword == "relay-secret"));
    }

    [TestMethod]
    public async Task ResolveAsync_ExpandsGlobalRelayerAddressesInHostAddressOrderAndDeduplicates()
    {
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["first.example"] =
            [
                IPAddress.Parse("192.0.2.1"),
                IPAddress.Parse("2001:db8::1"),
                IPAddress.Parse("192.0.2.1")
            ],
            ["second.example"] =
            [
                IPAddress.Parse("192.0.2.1"),
                IPAddress.Parse("192.0.2.2")
            ]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);
        var target = CreateGlobalRelayerTarget(" first.example | second.example ");

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);
        var candidates = endpoint.GetCandidates();

        CollectionAssert.AreEqual(
            new[] { "first.example", "first.example", "second.example" },
            candidates.Select(static candidate => candidate.Host).ToArray());
        CollectionAssert.AreEqual(
            new[] { "192.0.2.1", "2001:db8::1", "192.0.2.2" },
            candidates.Select(static candidate => candidate.ConnectionAddress).ToArray());
        CollectionAssert.AreEqual(
            new[] { "first.example", "second.example" },
            addressResolver.RequestedHosts.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_AppliesGlobalRelayerMxCapAfterAddressFlattening()
    {
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["first.example"] = [IPAddress.Parse("192.0.2.1"), IPAddress.Parse("192.0.2.2")],
            ["second.example"] = [IPAddress.Parse("192.0.2.3"), IPAddress.Parse("192.0.2.4")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);
        var target = CreateGlobalRelayerTarget(
            "first.example|second.example",
            maxNumberOfMxHosts: 3);

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "192.0.2.1", "192.0.2.2", "192.0.2.3" },
            endpoint.GetCandidates().Select(static candidate => candidate.ConnectionAddress).ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_GlobalRelayerConfiguredIpBypassesDns()
    {
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["relay.example"] = [IPAddress.Parse("192.0.2.2")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);
        var target = CreateGlobalRelayerTarget("192.0.2.1|relay.example");

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "192.0.2.1", "192.0.2.2" },
            endpoint.GetCandidates().Select(static candidate => candidate.ConnectionAddress).ToArray());
        CollectionAssert.AreEqual(new[] { "relay.example" }, addressResolver.RequestedHosts.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsLiteralGlobalRelayerActiveListener()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>()),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = CreateGlobalRelayerTarget(IPAddress.Loopback.ToString(), maxNumberOfMxHosts: 1);

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);
        var policy = new RemoteSmtpLocalEndpointPolicy(() =>
            [new IPEndPoint(IPAddress.Loopback, 25)]);

        Assert.IsTrue(endpoint.GetCandidates().Single().EnforceLocalEndpointGuard);
        Assert.ThrowsExactly<RemoteSmtpLocalEndpointDeniedException>(() => policy.EnsureAllowed(endpoint));
    }

    [TestMethod]
    public async Task ResolveAsync_GlobalRelayerDnsFailureReturnsIOException()
    {
        var addressResolver = new FakeAddressResolver(
            new IOException("DNS failure"));
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);
        var target = CreateGlobalRelayerTarget("relay.example");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => resolver.ResolveAsync(target, CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ResolveAsync_GlobalRelayerRetainsHostnameForTlsAndSni()
    {
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["relay.example"] = [IPAddress.Parse("192.0.2.1")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);
        var target = CreateGlobalRelayerTarget(
            "relay.example",
            connectionSecurity: (int)RemoteSmtpConnectionSecurity.Ssl);

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);
        var candidate = endpoint.GetCandidates().Single();

        Assert.AreEqual("relay.example", candidate.Host);
        Assert.AreEqual("192.0.2.1", candidate.ConnectionAddress);
    }

    [TestMethod]
    public async Task ResolveAsync_GlobalRelayerCandidatesInheritRuleBindAddress()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            CreateAddressResolver("relay.example"),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = CreateGlobalRelayerTarget("relay.example");

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);
        var candidates = (endpoint with { LocalBindAddress = "192.0.2.10" }).GetCandidates();

        Assert.AreEqual("192.0.2.10", candidates.Single().LocalBindAddress);
    }

    private static DeliveryTarget CreateGlobalRelayerTarget(
        string targetHost,
        int maxNumberOfMxHosts = 0,
        int connectionSecurity = (int)RemoteSmtpConnectionSecurity.None) =>
        new(
            DeliveryTargetKind.Route,
            "relayer:" + targetHost,
            "one.example",
            Route: new SmtpRouteResolution(
                0,
                "*",
                targetHost,
                25,
                connectionSecurity,
                false),
            MaxNumberOfMxHosts: maxNumberOfMxHosts);

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task ResolveAsync_MapsRelayerConnectionSecurity(int security)
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            CreateAddressResolver("relay.example"),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "relayer:relay.example",
            "one.example",
            Route: new SmtpRouteResolution(
                0,
                "*",
                "relay.example",
                25,
                security,
                false));

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual((RemoteSmtpConnectionSecurity)security, endpoint.ConnectionSecurity);
    }

    [TestMethod]
    public async Task ResolveAsync_UsesLowestPreferenceMxAndCachesResult()
    {
        var mxResolver = new FakeMxResolver(
            new DnsMxRecord("mx20.example.net.", 20, TimeSpan.FromMinutes(10)),
            new DnsMxRecord("mx10.example.net.", 10, TimeSpan.FromMinutes(10)));
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
            CreateAddressResolver("mx20.example.net", "mx10.example.net"),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            "remote:example.net",
            "example.net");

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);
        var cachedEndpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual("mx10.example.net", endpoint.Host);
        Assert.AreEqual("mx10.example.net", cachedEndpoint.Host);
        Assert.AreEqual(1, mxResolver.CallCount);
        Assert.AreEqual(25, endpoint.Port);
        Assert.AreEqual(RemoteSmtpConnectionSecurity.None, endpoint.ConnectionSecurity);
    }

    [TestMethod]
    public async Task ResolveAsync_PreservesMxOrderAndAppliesMaxHostLimit()
    {
        var mxResolver = new FakeMxResolver(
            new DnsMxRecord("mx30.example.net.", 30, TimeSpan.FromMinutes(10)),
            new DnsMxRecord("mx10.example.net.", 10, TimeSpan.FromMinutes(10)),
            new DnsMxRecord("mx20.example.net.", 20, TimeSpan.FromMinutes(10)));
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["mx10.example.net"] = [IPAddress.Parse("192.0.2.10")],
            ["mx20.example.net"] = [IPAddress.Parse("192.0.2.20")],
            ["mx30.example.net"] = [IPAddress.Parse("192.0.2.30")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            "remote:example.net",
            "example.net",
            MaxNumberOfMxHosts: 2);

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "mx10.example.net", "mx20.example.net" },
            endpoint.GetCandidates().Select(static candidate => candidate.Host).ToArray());
        Assert.AreEqual(1, mxResolver.CallCount);
    }

    [TestMethod]
    public async Task ResolveAsync_FlattensAddressOrderAndDeduplicatesAcrossMxHosts()
    {
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["mx10.example.net"] =
            [
                IPAddress.Parse("192.0.2.10"),
                IPAddress.Parse("2001:db8::10"),
                IPAddress.Parse("192.0.2.10")
            ],
            ["mx20.example.net"] =
            [
                IPAddress.Parse("192.0.2.10"),
                IPAddress.Parse("192.0.2.20")
            ]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(
                new DnsMxRecord("mx20.example.net.", 20, TimeSpan.FromMinutes(10)),
                new DnsMxRecord("mx10.example.net.", 10, TimeSpan.FromMinutes(10))),
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);

        var endpoint = await resolver.ResolveAsync(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:example.net", "example.net"),
            CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "mx10.example.net", "mx10.example.net", "mx20.example.net" },
            endpoint.GetCandidates().Select(static candidate => candidate.Host).ToArray());
        CollectionAssert.AreEqual(
            new[] { "192.0.2.10", "2001:db8::10", "192.0.2.20" },
            endpoint.GetCandidates().Select(static candidate => candidate.ConnectionAddress).ToArray());
        CollectionAssert.AreEqual(
            new[] { "mx10.example.net", "mx20.example.net" },
            addressResolver.RequestedHosts.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_AppliesMxCapAfterFlatteningAddresses()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(
                new DnsMxRecord("mx10.example.net.", 10, TimeSpan.FromMinutes(10)),
                new DnsMxRecord("mx20.example.net.", 20, TimeSpan.FromMinutes(10))),
            new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
            {
                ["mx10.example.net"] = [IPAddress.Parse("192.0.2.10"), IPAddress.Parse("192.0.2.11")],
                ["mx20.example.net"] = [IPAddress.Parse("192.0.2.20"), IPAddress.Parse("192.0.2.21")]
            }),
            RemoteSmtpEndpointResolverOptions.Default);

        var endpoint = await resolver.ResolveAsync(
            new DeliveryTarget(
                DeliveryTargetKind.RemoteDomain,
                "remote:example.net",
                "example.net",
                MaxNumberOfMxHosts: 3),
            CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "192.0.2.10", "192.0.2.11", "192.0.2.20" },
            endpoint.GetCandidates().Select(static candidate => candidate.ConnectionAddress).ToArray());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task ResolveAsync_MapsConfiguredRemoteConnectionSecurity(int security)
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(new DnsMxRecord("mx.example.net.", 10, TimeSpan.FromMinutes(10))),
            CreateAddressResolver("mx.example.net"),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            "remote:example.net",
            "example.net",
            RemoteConnectionSecurity: security,
            VerifyRemoteSslCertificate: true);

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual((RemoteSmtpConnectionSecurity)security, endpoint.ConnectionSecurity);
        Assert.IsTrue(endpoint.VerifyRemoteSslCertificate);
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsInvalidGlobalConnectionSecurity()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(new DnsMxRecord("mx.example.net.", 10, TimeSpan.FromMinutes(10))),
            CreateAddressResolver("mx.example.net"),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            "remote:example.net",
            "example.net",
            RemoteConnectionSecurity: 4);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(target, CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ResolveAsync_RouteSecurityOverridesRemoteConnectionSecurity()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            CreateAddressResolver("relay.customer.example"),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.Route,
            "route:5",
            "customer.example",
            Route: new SmtpRouteResolution(
                RouteId: 5,
                DomainName: "*.customer.example",
                TargetHost: "relay.customer.example",
                TargetPort: 2525,
                ConnectionSecurity: (int)RemoteSmtpConnectionSecurity.StartTlsRequired,
                TreatRecipientAsLocal: false,
                RequiresAuthentication: false,
                AuthenticationUsername: "",
                AuthenticationPassword: ""),
            RemoteConnectionSecurity: (int)RemoteSmtpConnectionSecurity.None);

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual(RemoteSmtpConnectionSecurity.StartTlsRequired, endpoint.ConnectionSecurity);
    }

    [TestMethod]
    public async Task ResolveAsync_FallsBackToDomainWhenMxIsEmpty()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
            CreateAddressResolver("example.net"),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            "remote:example.net",
            "example.net");

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual("example.net", endpoint.Host);
        Assert.AreEqual(25, endpoint.Port);
        Assert.AreEqual(RemoteSmtpConnectionSecurity.None, endpoint.ConnectionSecurity);
        Assert.AreEqual("192.0.2.1", endpoint.ConnectionAddress);
        Assert.AreEqual("192.0.2.1", endpoint.GetCandidates().Single().ConnectionAddress);
    }

    [TestMethod]
    public async Task ResolveAsync_UsesSingleCnameTargetForTlsAndAddressResolution()
    {
        var cnameResolver = new FakeMxAndCnameResolver(
            new Dictionary<string, IReadOnlyList<DnsCnameRecord>>
            {
                ["example.net"] = [new DnsCnameRecord("target.example.net.", TimeSpan.FromMinutes(10))]
            });
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["target.example.net"] = [IPAddress.Parse("192.0.2.25")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            cnameResolver,
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);

        var endpoint = await resolver.ResolveAsync(
            new DeliveryTarget(
                DeliveryTargetKind.RemoteDomain,
                "remote:example.net",
                "example.net",
                RemoteConnectionSecurity: (int)RemoteSmtpConnectionSecurity.Ssl),
            CancellationToken.None);

        var candidate = endpoint.GetCandidates().Single();
        Assert.AreEqual("target.example.net", candidate.Host);
        Assert.AreEqual("192.0.2.25", candidate.ConnectionAddress);
        Assert.AreEqual(RemoteSmtpConnectionSecurity.Ssl, candidate.ConnectionSecurity);
        CollectionAssert.AreEqual(new[] { "target.example.net" }, addressResolver.RequestedHosts.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_DoesNotRecurseWhenMultipleCnameRecordsExist()
    {
        var cnameResolver = new FakeMxAndCnameResolver(
            new Dictionary<string, IReadOnlyList<DnsCnameRecord>>
            {
                ["example.net"] =
                [
                    new DnsCnameRecord("first.example.net.", TimeSpan.FromMinutes(10)),
                    new DnsCnameRecord("second.example.net.", TimeSpan.FromMinutes(10))
                ]
            });
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["example.net"] = [IPAddress.Parse("192.0.2.1")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            cnameResolver,
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);

        var endpoint = await resolver.ResolveAsync(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:example.net", "example.net"),
            CancellationToken.None);

        Assert.AreEqual("example.net", endpoint.Host);
        CollectionAssert.AreEqual(new[] { "example.net" }, addressResolver.RequestedHosts.ToArray());
        CollectionAssert.AreEqual(new[] { "example.net" }, cnameResolver.RequestedDomains.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_FailsClosedWhenCnameRecursionExceedsBound()
    {
        var cnameRecords = Enumerable.Range(0, 10)
            .ToDictionary(
                index => $"alias{index}.example.net",
                index => (IReadOnlyList<DnsCnameRecord>)[
                    new DnsCnameRecord($"alias{index + 1}.example.net", TimeSpan.FromMinutes(10))]);
        var cnameResolver = new FakeMxAndCnameResolver(cnameRecords);
        var resolver = new RemoteSmtpEndpointResolver(
            cnameResolver,
            new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>()),
            RemoteSmtpEndpointResolverOptions.Default);

        await Assert.ThrowsExactlyAsync<IOException>(
            () => resolver.ResolveAsync(
                new DeliveryTarget(
                    DeliveryTargetKind.RemoteDomain,
                    "remote:alias0.example.net",
                    "alias0.example.net"),
                CancellationToken.None).AsTask());

        Assert.IsTrue(cnameResolver.RequestedDomains.Count <= 11);
        Assert.IsFalse(cnameResolver.RequestedDomains.Contains("alias11.example.net"));
    }

    [TestMethod]
    public async Task ResolveAsync_DoesNotFallBackToDomainWhenMxIsNull()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(new DnsMxRecord(".", 0, TimeSpan.FromMinutes(10))),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            "remote:example.net",
            "example.net");

        var exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => resolver.ResolveAsync(target, CancellationToken.None).AsTask());

        StringAssert.Contains(exception.Message, "null MX");
    }

    [TestMethod]
    public async Task ResolveAsync_UsesLiteralMxIpWithoutAddressLookup()
    {
        var addressResolver = new FakeAddressResolver(
            new Dictionary<string, IReadOnlyList<IPAddress>>());
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(new DnsMxRecord("192.0.2.25.", 10, TimeSpan.FromMinutes(10))),
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);

        var endpoint = await resolver.ResolveAsync(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:example.net", "example.net"),
            CancellationToken.None);
        var candidate = endpoint.GetCandidates().Single();

        Assert.AreEqual("192.0.2.25", candidate.Host);
        Assert.AreEqual("192.0.2.25", candidate.ConnectionAddress);
        Assert.AreEqual(0, addressResolver.RequestedHosts.Count);
    }

    [TestMethod]
    public async Task ResolveAsync_AddressLookupFailureReturnsIOException()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(new DnsMxRecord("mx.example.net.", 10, TimeSpan.FromMinutes(10))),
            new FakeAddressResolver(new IOException("DNS failure")),
            RemoteSmtpEndpointResolverOptions.Default);

        await Assert.ThrowsExactlyAsync<IOException>(
            () => resolver.ResolveAsync(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:example.net", "example.net"),
                CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ResolveAsync_NoUsableMxAddressReturnsIOException()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(new DnsMxRecord("mx.example.net.", 10, TimeSpan.FromMinutes(10))),
            new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
            {
                ["mx.example.net"] = []
            }),
            RemoteSmtpEndpointResolverOptions.Default);

        await Assert.ThrowsExactlyAsync<IOException>(
            () => resolver.ResolveAsync(
                new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:example.net", "example.net"),
                CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ResolveAsync_RetainsMxHostForTlsAndSni()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(new DnsMxRecord("mx.example.net.", 10, TimeSpan.FromMinutes(10))),
            new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
            {
                ["mx.example.net"] = [IPAddress.Parse("192.0.2.25")]
            }),
            RemoteSmtpEndpointResolverOptions.Default);

        var endpoint = await resolver.ResolveAsync(
            new DeliveryTarget(
                DeliveryTargetKind.RemoteDomain,
                "remote:example.net",
                "example.net",
                RemoteConnectionSecurity: (int)RemoteSmtpConnectionSecurity.Ssl),
            CancellationToken.None);
        var candidate = endpoint.GetCandidates().Single();

        Assert.AreEqual("mx.example.net", candidate.Host);
        Assert.AreEqual("192.0.2.25", candidate.ConnectionAddress);
        Assert.AreEqual(RemoteSmtpConnectionSecurity.Ssl, candidate.ConnectionSecurity);
    }

    [TestMethod]
    public async Task ResolveAsync_UsesNonNullMxInsteadOfDomainFallback()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(new DnsMxRecord("mx.example.net.", 10, TimeSpan.FromMinutes(10))),
            CreateAddressResolver("mx.example.net"),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            "remote:example.net",
            "example.net");

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual("mx.example.net", endpoint.Host);
    }

    [TestMethod]
    public async Task ResolveAsync_NoMxFollowsSingleCnameBeforeAddressResolution()
    {
        var mxResolver = new FakeCnameMxResolver(
            new Dictionary<string, IReadOnlyList<DnsMxRecord>>
            {
                ["alias.example.net"] = [],
                ["canonical.example.net"] = []
            },
            new Dictionary<string, IReadOnlyList<DnsCnameRecord>>
            {
                ["alias.example.net"] =
                [new DnsCnameRecord("canonical.example.net", TimeSpan.FromMinutes(5))]
            });
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["canonical.example.net"] = [IPAddress.Parse("192.0.2.31")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);

        var endpoint = await resolver.ResolveAsync(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:alias.example.net", "alias.example.net"),
            CancellationToken.None);

        Assert.AreEqual("canonical.example.net", endpoint.Host);
        Assert.AreEqual("192.0.2.31", endpoint.ConnectionAddress);
        CollectionAssert.AreEqual(new[] { "canonical.example.net" }, addressResolver.RequestedHosts.ToArray());
        CollectionAssert.AreEqual(new[] { "alias.example.net", "canonical.example.net" }, mxResolver.RequestedMxHosts.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_NoCnameUsesOriginalDomainImplicitAddresses()
    {
        var mxResolver = new FakeCnameMxResolver(
            new Dictionary<string, IReadOnlyList<DnsMxRecord>>
            {
                ["alias.example.net"] = []
            },
            new Dictionary<string, IReadOnlyList<DnsCnameRecord>>
            {
                ["alias.example.net"] = []
            });
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["alias.example.net"] = [IPAddress.Parse("192.0.2.32")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);

        var endpoint = await resolver.ResolveAsync(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:alias.example.net", "alias.example.net"),
            CancellationToken.None);

        Assert.AreEqual("alias.example.net", endpoint.Host);
        Assert.AreEqual("192.0.2.32", endpoint.ConnectionAddress);
        CollectionAssert.AreEqual(new[] { "alias.example.net" }, addressResolver.RequestedHosts.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_MultipleCnamesUsesOriginalDomainImplicitAddresses()
    {
        var mxResolver = new FakeCnameMxResolver(
            new Dictionary<string, IReadOnlyList<DnsMxRecord>>
            {
                ["alias.example.net"] = []
            },
            new Dictionary<string, IReadOnlyList<DnsCnameRecord>>
            {
                ["alias.example.net"] =
                [
                    new DnsCnameRecord("one.example.net", TimeSpan.FromMinutes(5)),
                    new DnsCnameRecord("two.example.net", TimeSpan.FromMinutes(5))
                ]
            });
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["alias.example.net"] = [IPAddress.Parse("192.0.2.33")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);

        var endpoint = await resolver.ResolveAsync(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:alias.example.net", "alias.example.net"),
            CancellationToken.None);

        Assert.AreEqual("alias.example.net", endpoint.Host);
        CollectionAssert.AreEqual(new[] { "alias.example.net" }, addressResolver.RequestedHosts.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_CnameLookupFailureUsesOriginalDomainImplicitAddresses()
    {
        var mxResolver = new FakeCnameMxResolver(
            new Dictionary<string, IReadOnlyList<DnsMxRecord>>
            {
                ["alias.example.net"] = []
            },
            new Dictionary<string, IReadOnlyList<DnsCnameRecord>>())
        {
            CnameFailure = new IOException("CNAME lookup failed")
        };
        var addressResolver = new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>
        {
            ["alias.example.net"] = [IPAddress.Parse("192.0.2.34")]
        });
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
            addressResolver,
            RemoteSmtpEndpointResolverOptions.Default);

        var endpoint = await resolver.ResolveAsync(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:alias.example.net", "alias.example.net"),
            CancellationToken.None);

        Assert.AreEqual("alias.example.net", endpoint.Host);
        Assert.AreEqual("192.0.2.34", endpoint.ConnectionAddress);
    }

    [TestMethod]
    public async Task ResolveAsync_CnameCycleFailsClosed()
    {
        var mxResolver = new FakeCnameMxResolver(
            new Dictionary<string, IReadOnlyList<DnsMxRecord>>
            {
                ["one.example.net"] = [],
                ["two.example.net"] = []
            },
            new Dictionary<string, IReadOnlyList<DnsCnameRecord>>
            {
                ["one.example.net"] = [new DnsCnameRecord("two.example.net", TimeSpan.FromMinutes(5))],
                ["two.example.net"] = [new DnsCnameRecord("one.example.net", TimeSpan.FromMinutes(5))]
            });
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
            new FakeAddressResolver(new Dictionary<string, IReadOnlyList<IPAddress>>()),
            RemoteSmtpEndpointResolverOptions.Default);

        await Assert.ThrowsExactlyAsync<IOException>(() => resolver.ResolveAsync(
            new DeliveryTarget(DeliveryTargetKind.RemoteDomain, "remote:one.example.net", "one.example.net"),
            CancellationToken.None).AsTask());
    }

    private static IDnsAddressResolver CreateAddressResolver(params string[] hosts) =>
        new FakeAddressResolver(
            hosts.ToDictionary(
                static host => host,
                static _ => (IReadOnlyList<IPAddress>)[IPAddress.Parse("192.0.2.1")]));

    private sealed class FakeMxResolver : IDnsMxResolver
    {
        private readonly IReadOnlyList<DnsMxRecord> _records;

        public FakeMxResolver(params DnsMxRecord[] records)
        {
            _records = records;
        }

        public int CallCount { get; private set; }

        public ValueTask<IReadOnlyList<DnsMxRecord>> ResolveMxAsync(
            string domainName,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(_records);
        }
    }

    private sealed class FakeCnameMxResolver : IDnsMxResolver, IDnsCnameResolver
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<DnsMxRecord>> _mxRecords;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<DnsCnameRecord>> _cnameRecords;

        public FakeCnameMxResolver(
            IReadOnlyDictionary<string, IReadOnlyList<DnsMxRecord>> mxRecords,
            IReadOnlyDictionary<string, IReadOnlyList<DnsCnameRecord>> cnameRecords)
        {
            _mxRecords = mxRecords;
            _cnameRecords = cnameRecords;
        }

        public List<string> RequestedMxHosts { get; } = [];

        public Exception? CnameFailure { get; init; }

        public ValueTask<IReadOnlyList<DnsMxRecord>> ResolveMxAsync(
            string domainName,
            CancellationToken cancellationToken)
        {
            RequestedMxHosts.Add(domainName);
            return ValueTask.FromResult(_mxRecords.GetValueOrDefault(domainName, []));
        }

        public ValueTask<IReadOnlyList<DnsCnameRecord>> ResolveCnameAsync(
            string domainName,
            CancellationToken cancellationToken)
        {
            if (CnameFailure is not null)
            {
                throw CnameFailure;
            }

            return ValueTask.FromResult(_cnameRecords.GetValueOrDefault(domainName, []));
        }
    }

    private sealed class FakeMxAndCnameResolver : IDnsMxResolver, IDnsCnameResolver
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<DnsCnameRecord>> _records;

        public FakeMxAndCnameResolver(IReadOnlyDictionary<string, IReadOnlyList<DnsCnameRecord>> records)
        {
            _records = records;
        }

        public List<string> RequestedDomains { get; } = [];

        public ValueTask<IReadOnlyList<DnsMxRecord>> ResolveMxAsync(
            string domainName,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DnsMxRecord>>([]);

        public ValueTask<IReadOnlyList<DnsCnameRecord>> ResolveCnameAsync(
            string domainName,
            CancellationToken cancellationToken)
        {
            RequestedDomains.Add(domainName);
            return ValueTask.FromResult(
                _records.TryGetValue(domainName, out var records)
                    ? records
                    : (IReadOnlyList<DnsCnameRecord>)[]);
        }
    }

    private sealed class FakeAddressResolver : IDnsAddressResolver
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<IPAddress>>? _addresses;
        private readonly IReadOnlyDictionary<string, Exception>? _failures;
        private readonly Exception? _failure;

        public FakeAddressResolver(IReadOnlyDictionary<string, IReadOnlyList<IPAddress>> addresses)
        {
            _addresses = addresses;
        }

        public FakeAddressResolver(
            IReadOnlyDictionary<string, IReadOnlyList<IPAddress>> addresses,
            IReadOnlyDictionary<string, Exception> failures)
        {
            _addresses = addresses;
            _failures = failures;
        }

        public FakeAddressResolver(Exception failure)
        {
            _failure = failure;
        }

        public List<string> RequestedHosts { get; } = [];

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(
            string hostName,
            CancellationToken cancellationToken)
        {
            RequestedHosts.Add(hostName);
            if (_failure is not null)
            {
                throw _failure;
            }

            if (_failures is not null && _failures.TryGetValue(hostName, out var failure))
            {
                throw failure;
            }

            return ValueTask.FromResult(_addresses![hostName]);
        }
    }
}
