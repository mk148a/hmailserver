using System.Net;
using System.Net.Sockets;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SpfEvaluatorTests
{
    [TestMethod]
    public void ResultModel_PreservesTheSevenRfc7208Results()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                SpfResult.None,
                SpfResult.Neutral,
                SpfResult.Pass,
                SpfResult.Fail,
                SpfResult.SoftFail,
                SpfResult.TempError,
                SpfResult.PermError
            },
            Enum.GetValues<SpfResult>());
    }

    [TestMethod]
    [DataRow("v=spf1 +all", SpfResult.Pass)]
    [DataRow("v=spf1 -all", SpfResult.Fail)]
    [DataRow("v=spf1 ~all", SpfResult.SoftFail)]
    [DataRow("v=spf1 ?all", SpfResult.Neutral)]
    [DataRow("v=spf1", SpfResult.Neutral)]
    public async Task EvaluateAsync_MapsQualifiersAndImplicitNeutralDefault(
        string record,
        SpfResult expected)
    {
        var resolver = new FakeSpfDnsResolver().AddTxt("example.test", record);

        var result = await EvaluateAsync(resolver, "192.0.2.1");

        Assert.AreEqual(expected, result.Result);
    }

    [TestMethod]
    public async Task EvaluateAsync_UsesRfc7208Ipv4CidrExample()
    {
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 ip4:192.0.2.128/28 -all");
        var evaluator = new SpfEvaluator(resolver);

        var passing = await evaluator.EvaluateAsync(
            CreateRequest("192.0.2.129"),
            CancellationToken.None);
        var failing = await evaluator.EvaluateAsync(
            CreateRequest("192.0.2.65"),
            CancellationToken.None);

        Assert.AreEqual(SpfResult.Pass, passing.Result);
        Assert.AreEqual("ip4:192.0.2.128/28", passing.MatchedMechanism);
        Assert.AreEqual(SpfResult.Fail, failing.Result);
    }

    [TestMethod]
    public async Task EvaluateAsync_MatchesIpv6NetworksWithoutMatchingIpv4Clients()
    {
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 ip6:2001:db8::/32 -all");
        var evaluator = new SpfEvaluator(resolver);

        var passing = await evaluator.EvaluateAsync(
            CreateRequest("2001:db8::cb01"),
            CancellationToken.None);
        var failing = await evaluator.EvaluateAsync(
            CreateRequest("192.0.2.1"),
            CancellationToken.None);

        Assert.AreEqual(SpfResult.Pass, passing.Result);
        Assert.AreEqual(SpfResult.Fail, failing.Result);
    }

    [TestMethod]
    public async Task EvaluateAsync_SelectsExactlyOneSpfRecordAndValidatesEntireRecordBeforeEvaluation()
    {
        var noPolicy = await EvaluateAsync(new FakeSpfDnsResolver(), "192.0.2.1");
        var wrongVersion = await EvaluateAsync(
            new FakeSpfDnsResolver().AddTxt("example.test", "v=spf10 -all"),
            "192.0.2.1");
        var duplicate = await EvaluateAsync(
            new FakeSpfDnsResolver().AddTxt(
                "example.test",
                "v=spf1 -all",
                "v=spf1 +all"),
            "192.0.2.1");
        var invalidAfterAll = await EvaluateAsync(
            new FakeSpfDnsResolver().AddTxt("example.test", "v=spf1 +all unknown-mechanism"),
            "192.0.2.1");

        Assert.AreEqual(SpfResult.None, noPolicy.Result);
        Assert.AreEqual(SpfResult.None, wrongVersion.Result);
        Assert.AreEqual(SpfResult.PermError, duplicate.Result);
        Assert.AreEqual(SpfResult.PermError, invalidAfterAll.Result);
    }

    [TestMethod]
    [DataRow("v=spf1 ip4:192.0.2.0/33 -all")]
    [DataRow("v=spf1 a/33 -all")]
    [DataRow("v=spf1 a/24/64 -all")]
    [DataRow("v=spf1 redirect=one.example.test redirect=two.example.test")]
    [DataRow("v=spf1 exists:%(ir).example.test -all")]
    [DataRow("v=spf1 include:child.example.test/24 -all")]
    public async Task EvaluateAsync_ReturnsPermErrorForInvalidRecordSyntax(string record)
    {
        var resolver = new FakeSpfDnsResolver().AddTxt("example.test", record);

        var result = await EvaluateAsync(resolver, "192.0.2.1");

        Assert.AreEqual(SpfResult.PermError, result.Result);
    }

    [TestMethod]
    public async Task EvaluateAsync_AppliesRfcDualCidrLengthsToAddressMechanism()
    {
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 a/24//64 -all")
            .AddAddresses("example.test", AddressFamily.InterNetwork, "192.0.2.130")
            .AddAddresses("example.test", AddressFamily.InterNetworkV6, "2001:db8::2");
        var evaluator = new SpfEvaluator(resolver);

        var ipv4 = await evaluator.EvaluateAsync(
            CreateRequest("192.0.2.129"),
            CancellationToken.None);
        var ipv6 = await evaluator.EvaluateAsync(
            CreateRequest("2001:db8::1"),
            CancellationToken.None);

        Assert.AreEqual(SpfResult.Pass, ipv4.Result);
        Assert.AreEqual(SpfResult.Pass, ipv6.Result);
    }

    [TestMethod]
    public async Task EvaluateAsync_MapsDnsTemporaryFailureAndInternalTimeoutToTempError()
    {
        var temporaryResolver = new FakeSpfDnsResolver()
            .SetTxtResponse("example.test", SpfDnsResponse<string>.TemporaryError());
        var delayedResolver = new DelayedSpfDnsResolver();

        var temporary = await EvaluateAsync(temporaryResolver, "192.0.2.1");
        var timedOut = await new SpfEvaluator(
                delayedResolver,
                new SpfEvaluatorOptions { EvaluationTimeout = TimeSpan.FromMilliseconds(20) })
            .EvaluateAsync(CreateRequest("192.0.2.1"), CancellationToken.None);

        Assert.AreEqual(SpfResult.TempError, temporary.Result);
        Assert.AreEqual(SpfResult.TempError, timedOut.Result);
    }

    [TestMethod]
    public async Task EvaluateAsync_ImplementsIncludeAndRedirectResultSemantics()
    {
        var includeResolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 include:_spf.example.test -all")
            .AddTxt("_spf.example.test", "v=spf1 ip4:192.0.2.1 -all");
        var redirectResolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 redirect=_spf.example.test")
            .AddTxt("_spf.example.test", "v=spf1 ~all");
        var missingIncludeResolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 include:missing.example.test -all");

        var included = await EvaluateAsync(includeResolver, "192.0.2.1");
        var redirected = await EvaluateAsync(redirectResolver, "192.0.2.1");
        var missing = await EvaluateAsync(missingIncludeResolver, "192.0.2.1");

        Assert.AreEqual(SpfResult.Pass, included.Result);
        Assert.AreEqual("include:_spf.example.test", included.MatchedMechanism);
        Assert.AreEqual(SpfResult.SoftFail, redirected.Result);
        Assert.AreEqual(SpfResult.PermError, missing.Result);
    }

    [TestMethod]
    public async Task EvaluateAsync_SupportsAddressMxExistsAndValidatedPtrMechanisms()
    {
        var addressResolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 a:mail.example.test/28 -all")
            .AddAddresses("mail.example.test", AddressFamily.InterNetwork, "192.0.2.130");
        var mxResolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 mx -all")
            .AddMx("example.test", "mx.example.test")
            .AddAddresses("mx.example.test", AddressFamily.InterNetwork, "192.0.2.1");
        var existsResolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 exists:3.2.0.192.allow.example.test -all")
            .AddAddresses("3.2.0.192.allow.example.test", AddressFamily.InterNetwork, "127.0.0.2");
        var ptrResolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 ptr:example.test -all")
            .AddPtr("192.0.2.1", "mail.example.test")
            .AddAddresses("mail.example.test", AddressFamily.InterNetwork, "192.0.2.1");

        var address = await EvaluateAsync(addressResolver, "192.0.2.129");
        var mx = await EvaluateAsync(mxResolver, "192.0.2.1");
        var exists = await EvaluateAsync(existsResolver, "192.0.2.3");
        var ptr = await EvaluateAsync(ptrResolver, "192.0.2.1");

        Assert.AreEqual(SpfResult.Pass, address.Result);
        Assert.AreEqual(SpfResult.Pass, mx.Result);
        Assert.AreEqual(SpfResult.Pass, exists.Result);
        Assert.AreEqual(SpfResult.Pass, ptr.Result);
    }

    [TestMethod]
    public async Task EvaluateAsync_ExpandsRfc7208DomainMacros()
    {
        const string expanded = "3.2.0.192.in-addr._spf.example.com";
        var resolver = new FakeSpfDnsResolver()
            .AddTxt(
                "email.example.com",
                "v=spf1 exists:%{ir}.%{v}._spf.%{d2} -all")
            .AddAddresses(expanded, AddressFamily.InterNetwork, "127.0.0.2");
        var evaluator = new SpfEvaluator(resolver);

        var result = await evaluator.EvaluateAsync(
            new SpfEvaluationRequest(
                IPAddress.Parse("192.0.2.3"),
                "email.example.com",
                "strong-bad@email.example.com",
                "mail.example.com"),
            CancellationToken.None);

        Assert.AreEqual(SpfResult.Pass, result.Result);
        CollectionAssert.Contains(
            resolver.Queries.ToArray(),
            $"A:{expanded}");
    }

    [TestMethod]
    public async Task EvaluateAsync_ExpandsValidatedDomainMacroThroughBoundedPtrChecks()
    {
        const string existsTarget = "mail.example.test.allow.example.test";
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 exists:%{p}.allow.example.test -all")
            .AddPtr("192.0.2.1", "mail.example.test")
            .AddAddresses("mail.example.test", AddressFamily.InterNetwork, "192.0.2.1")
            .AddAddresses(existsTarget, AddressFamily.InterNetwork, "127.0.0.2");

        var result = await EvaluateAsync(resolver, "192.0.2.1");

        Assert.AreEqual(SpfResult.Pass, result.Result);
        Assert.AreEqual(2, result.DnsTermCount);
        CollectionAssert.Contains(resolver.Queries.ToArray(), $"A:{existsTarget}");
    }

    [TestMethod]
    public async Task EvaluateAsync_EnforcesGlobalTenDnsTermLimitAcrossIncludes()
    {
        var resolver = new FakeSpfDnsResolver();
        var includes = new List<string>();
        for (var index = 1; index <= 11; index++)
        {
            var domain = $"spf{index}.example.test";
            includes.Add("include:" + domain);
            resolver.AddTxt(domain, "v=spf1 ?all");
        }

        resolver.AddTxt("example.test", $"v=spf1 {string.Join(' ', includes)} -all");

        var result = await EvaluateAsync(resolver, "192.0.2.1");

        Assert.AreEqual(SpfResult.PermError, result.Result);
        Assert.AreEqual(11, result.DnsTermCount);
    }

    [TestMethod]
    public async Task EvaluateAsync_EnforcesDefaultTwoVoidLookupLimit()
    {
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 a:one.example.test a:two.example.test a:three.example.test -all");

        var result = await EvaluateAsync(resolver, "192.0.2.1");

        Assert.AreEqual(SpfResult.PermError, result.Result);
        Assert.AreEqual(3, result.VoidLookupCount);
    }

    [TestMethod]
    public async Task EvaluateAsync_EnforcesRecursionAndMxHostLimits()
    {
        var recursiveResolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 include:one.example.test -all")
            .AddTxt("one.example.test", "v=spf1 include:two.example.test -all")
            .AddTxt("two.example.test", "v=spf1 include:three.example.test -all")
            .AddTxt("three.example.test", "v=spf1 +all");
        var mxResolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 mx -all")
            .AddMx(
                "example.test",
                Enumerable.Range(1, 11).Select(index => $"mx{index}.example.test").ToArray());

        var recursive = await new SpfEvaluator(
                recursiveResolver,
                new SpfEvaluatorOptions { MaxRecursionDepth = 2 })
            .EvaluateAsync(CreateRequest("192.0.2.1"), CancellationToken.None);
        var tooManyMxHosts = await EvaluateAsync(mxResolver, "192.0.2.1");

        Assert.AreEqual(SpfResult.PermError, recursive.Result);
        Assert.AreEqual(SpfResult.PermError, tooManyMxHosts.Result);
    }

    [TestMethod]
    public async Task EvaluateAsync_ReturnsNoneForMalformedInitialDomainAndPermErrorForMalformedRedirect()
    {
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 redirect=localhost");
        var evaluator = new SpfEvaluator(resolver);

        var malformedInitial = await evaluator.EvaluateAsync(
            CreateRequest("192.0.2.1") with { Domain = "localhost" },
            CancellationToken.None);
        var malformedRedirect = await evaluator.EvaluateAsync(
            CreateRequest("192.0.2.1"),
            CancellationToken.None);

        Assert.AreEqual(SpfResult.None, malformedInitial.Result);
        Assert.AreEqual(SpfResult.PermError, malformedRedirect.Result);
    }

    private static async Task<SpfEvaluation> EvaluateAsync(
        ISpfDnsResolver resolver,
        string clientAddress)
    {
        return await new SpfEvaluator(resolver).EvaluateAsync(
            CreateRequest(clientAddress),
            CancellationToken.None);
    }

    private static SpfEvaluationRequest CreateRequest(string clientAddress) =>
        new(
            IPAddress.Parse(clientAddress),
            "example.test",
            "sender@example.test",
            "mail.example.test");

    private sealed class DelayedSpfDnsResolver : ISpfDnsResolver
    {
        public async ValueTask<SpfDnsResponse<string>> QueryTxtAsync(
            string domain,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return SpfDnsResponse<string>.NoData();
        }

        public ValueTask<SpfDnsResponse<IPAddress>> QueryAddressesAsync(
            string domain,
            AddressFamily addressFamily,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(SpfDnsResponse<IPAddress>.NoData());

        public ValueTask<SpfDnsResponse<SpfMxHost>> QueryMxAsync(
            string domain,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(SpfDnsResponse<SpfMxHost>.NoData());

        public ValueTask<SpfDnsResponse<string>> QueryPtrAsync(
            IPAddress address,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(SpfDnsResponse<string>.NoData());
    }

    private sealed class FakeSpfDnsResolver : ISpfDnsResolver
    {
        private readonly Dictionary<string, SpfDnsResponse<string>> _txt =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SpfDnsResponse<IPAddress>> _addresses =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SpfDnsResponse<SpfMxHost>> _mx =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SpfDnsResponse<string>> _ptr =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> Queries { get; } = [];

        public FakeSpfDnsResolver AddTxt(string domain, params string[] records) =>
            SetTxtResponse(domain, SpfDnsResponse<string>.Success(records));

        public FakeSpfDnsResolver SetTxtResponse(string domain, SpfDnsResponse<string> response)
        {
            _txt[Normalize(domain)] = response;
            return this;
        }

        public FakeSpfDnsResolver AddAddresses(
            string domain,
            AddressFamily family,
            params string[] addresses)
        {
            _addresses[AddressKey(domain, family)] =
                SpfDnsResponse<IPAddress>.Success(addresses.Select(IPAddress.Parse).ToArray());
            return this;
        }

        public FakeSpfDnsResolver AddMx(string domain, params string[] exchanges)
        {
            _mx[Normalize(domain)] = SpfDnsResponse<SpfMxHost>.Success(
                exchanges
                    .Select((exchange, index) => new SpfMxHost(exchange, (ushort)index))
                    .ToArray());
            return this;
        }

        public FakeSpfDnsResolver AddPtr(string address, params string[] names)
        {
            _ptr[IPAddress.Parse(address).ToString()] = SpfDnsResponse<string>.Success(names);
            return this;
        }

        public ValueTask<SpfDnsResponse<string>> QueryTxtAsync(
            string domain,
            CancellationToken cancellationToken)
        {
            Queries.Add("TXT:" + Normalize(domain));
            return ValueTask.FromResult(
                _txt.GetValueOrDefault(Normalize(domain), SpfDnsResponse<string>.NoData()));
        }

        public ValueTask<SpfDnsResponse<IPAddress>> QueryAddressesAsync(
            string domain,
            AddressFamily addressFamily,
            CancellationToken cancellationToken)
        {
            Queries.Add((addressFamily == AddressFamily.InterNetwork ? "A:" : "AAAA:") + Normalize(domain));
            return ValueTask.FromResult(
                _addresses.GetValueOrDefault(
                    AddressKey(domain, addressFamily),
                    SpfDnsResponse<IPAddress>.NoData()));
        }

        public ValueTask<SpfDnsResponse<SpfMxHost>> QueryMxAsync(
            string domain,
            CancellationToken cancellationToken)
        {
            Queries.Add("MX:" + Normalize(domain));
            return ValueTask.FromResult(
                _mx.GetValueOrDefault(Normalize(domain), SpfDnsResponse<SpfMxHost>.NoData()));
        }

        public ValueTask<SpfDnsResponse<string>> QueryPtrAsync(
            IPAddress address,
            CancellationToken cancellationToken)
        {
            Queries.Add("PTR:" + address);
            return ValueTask.FromResult(
                _ptr.GetValueOrDefault(address.ToString(), SpfDnsResponse<string>.NoData()));
        }

        private static string AddressKey(string domain, AddressFamily family) =>
            $"{family}:{Normalize(domain)}";

        private static string Normalize(string domain) =>
            domain.Trim().TrimEnd('.').ToLowerInvariant();
    }
}
