using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RemoteSmtpEndpointResolverTests
{
    [TestMethod]
    public async Task ResolveAsync_UsesConfiguredRouteTarget()
    {
        var mxResolver = new FakeMxResolver();
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
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
        Assert.AreEqual(0, mxResolver.CallCount);
    }

    [TestMethod]
    public async Task ResolveAsync_UsesRelayerTargetWithoutMx_AndDefaultsPort()
    {
        var mxResolver = new FakeMxResolver();
        var resolver = new RemoteSmtpEndpointResolver(
            mxResolver,
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
        Assert.AreEqual(0, mxResolver.CallCount);
    }

    [TestMethod]
    public async Task ResolveAsync_UsesAuthenticatedRelayerCredentials()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
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
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task ResolveAsync_MapsRelayerConnectionSecurity(int security)
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(),
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
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task ResolveAsync_MapsConfiguredRemoteConnectionSecurity(int security)
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(new DnsMxRecord("mx.example.net.", 10, TimeSpan.FromMinutes(10))),
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            "remote:example.net",
            "example.net",
            RemoteConnectionSecurity: security);

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual((RemoteSmtpConnectionSecurity)security, endpoint.ConnectionSecurity);
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsInvalidGlobalConnectionSecurity()
    {
        var resolver = new RemoteSmtpEndpointResolver(
            new FakeMxResolver(new DnsMxRecord("mx.example.net.", 10, TimeSpan.FromMinutes(10))),
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
            RemoteSmtpEndpointResolverOptions.Default);
        var target = new DeliveryTarget(
            DeliveryTargetKind.RemoteDomain,
            "remote:example.net",
            "example.net");

        var endpoint = await resolver.ResolveAsync(target, CancellationToken.None);

        Assert.AreEqual("example.net", endpoint.Host);
        Assert.AreEqual(25, endpoint.Port);
        Assert.AreEqual(RemoteSmtpConnectionSecurity.None, endpoint.ConnectionSecurity);
    }

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
}
