using System.Net;
using System.Net.Sockets;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SystemMailServerResolverTests
{
    [TestMethod]
    public void GetMailServer_ExtractsFinalDomainAndPreservesFirstSeenAddressOrder()
    {
        var dns = new FakeMailServerDnsResolver();
        dns.Mx["example.test"] = MailServerDnsResponse<MailServerMxHost>.Success(
            new MailServerMxHost("mx1.example.test", 10),
            new MailServerMxHost("mx2.example.test", 20));
        dns.SetAddresses(
            "mx1.example.test",
            AddressFamily.InterNetwork,
            IPAddress.Parse("192.0.2.10"),
            IPAddress.Parse("192.0.2.11"));
        dns.SetAddresses(
            "mx1.example.test",
            AddressFamily.InterNetworkV6,
            IPAddress.Parse("2001:db8::10"));
        dns.SetAddresses(
            "mx2.example.test",
            AddressFamily.InterNetwork,
            IPAddress.Parse("192.0.2.11"),
            IPAddress.Parse("192.0.2.12"));

        var resolver = new SystemMailServerResolver(dns, ipv6Available: true);

        Assert.AreEqual(
            "192.0.2.10,192.0.2.11,2001:db8::10,192.0.2.12",
            resolver.GetMailServer("display@local@Example.Test"));
        Assert.AreEqual("mx:Example.Test", dns.Calls[0]);
    }

    [TestMethod]
    public void GetMailServer_NullMxReturnsEmptyWithoutAddressLookup()
    {
        var dns = new FakeMailServerDnsResolver();
        dns.Mx["example.test"] = MailServerDnsResponse<MailServerMxHost>.Success(
            new MailServerMxHost(".", 0));
        var resolver = new SystemMailServerResolver(dns, ipv6Available: true);

        Assert.AreEqual(string.Empty, resolver.GetMailServer("user@example.test"));
        CollectionAssert.AreEqual(new[] { "mx:example.test" }, dns.Calls);
    }

    [TestMethod]
    public void GetMailServer_NoMxFollowsSingleCnameBeforeResolvingMxTargets()
    {
        var dns = new FakeMailServerDnsResolver();
        dns.Cnames["alias.example.test"] = MailServerDnsResponse<string>.Success(
            "canonical.example.test");
        dns.Mx["canonical.example.test"] = MailServerDnsResponse<MailServerMxHost>.Success(
            new MailServerMxHost("mx.example.test", 5));
        dns.SetAddresses(
            "mx.example.test",
            AddressFamily.InterNetwork,
            IPAddress.Parse("198.51.100.20"));
        var resolver = new SystemMailServerResolver(dns, ipv6Available: true);

        Assert.AreEqual(
            "198.51.100.20",
            resolver.GetMailServer("user@alias.example.test"));
        CollectionAssert.AreEqual(
            new[]
            {
                "mx:alias.example.test",
                "cname:alias.example.test",
                "mx:canonical.example.test",
                "a:mx.example.test",
                "aaaa:mx.example.test"
            },
            dns.Calls);
    }

    [TestMethod]
    public void GetMailServer_NoMxOrCnameUsesImplicitDomainAddresses()
    {
        var dns = new FakeMailServerDnsResolver();
        dns.SetAddresses(
            "example.test",
            AddressFamily.InterNetwork,
            IPAddress.Parse("203.0.113.30"));
        var resolver = new SystemMailServerResolver(dns, ipv6Available: true);

        Assert.AreEqual("203.0.113.30", resolver.GetMailServer("example.test"));
        CollectionAssert.AreEqual(
            new[]
            {
                "mx:example.test",
                "cname:example.test",
                "a:example.test",
                "aaaa:example.test"
            },
            dns.Calls);
    }

    [TestMethod]
    public void GetMailServer_MxAddressCnameAndPartialFailuresKeepResolvedTargets()
    {
        var dns = new FakeMailServerDnsResolver();
        dns.Mx["example.test"] = MailServerDnsResponse<MailServerMxHost>.Success(
            new MailServerMxHost("unresolved.example.test", 10),
            new MailServerMxHost("alias-mx.example.test", 20),
            new MailServerMxHost("192.0.2.50", 30));
        dns.SetAddressStatus(
            "unresolved.example.test",
            AddressFamily.InterNetwork,
            MailServerDnsResponse<IPAddress>.TemporaryError());
        dns.Cnames["alias-mx.example.test"] = MailServerDnsResponse<string>.Success(
            "real-mx.example.test");
        dns.SetAddresses(
            "real-mx.example.test",
            AddressFamily.InterNetwork,
            IPAddress.Parse("192.0.2.40"));
        var resolver = new SystemMailServerResolver(dns, ipv6Available: true);

        Assert.AreEqual(
            "192.0.2.40,192.0.2.50",
            resolver.GetMailServer("user@example.test"));
    }

    [TestMethod]
    public void GetMailServer_UnresolvedAndEmptyDomainsReturnEmpty()
    {
        var dns = new FakeMailServerDnsResolver();
        dns.Mx["missing.example.test"] =
            MailServerDnsResponse<MailServerMxHost>.NameError();
        var resolver = new SystemMailServerResolver(dns, ipv6Available: true);

        Assert.AreEqual(string.Empty, resolver.GetMailServer("user@missing.example.test"));
        Assert.AreEqual(string.Empty, resolver.GetMailServer("user@"));
        Assert.AreEqual(string.Empty, resolver.GetMailServer(string.Empty));
    }

    [TestMethod]
    public void GetMailServer_CnameRecursionStopsAfterLegacyLimit()
    {
        var dns = new FakeMailServerDnsResolver();
        for (var index = 0; index <= 10; index++)
        {
            dns.Cnames[$"alias{index}.example.test"] =
                MailServerDnsResponse<string>.Success($"alias{index + 1}.example.test");
        }

        var resolver = new SystemMailServerResolver(dns, ipv6Available: true);

        Assert.AreEqual(
            string.Empty,
            resolver.GetMailServer("user@alias0.example.test"));
        Assert.AreEqual(11, dns.Calls.Count(static call => call.StartsWith("mx:", StringComparison.Ordinal)));
        Assert.IsFalse(dns.Calls.Contains("mx:alias11.example.test"));
    }

    private sealed class FakeMailServerDnsResolver : IMailServerDnsResolver
    {
        private readonly Dictionary<string, MailServerDnsResponse<IPAddress>> _addresses =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, MailServerDnsResponse<MailServerMxHost>> Mx { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, MailServerDnsResponse<string>> Cnames { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> Calls { get; } = [];

        public ValueTask<MailServerDnsResponse<MailServerMxHost>> QueryMailServerMxAsync(
            string domain,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add($"mx:{domain}");
            return ValueTask.FromResult(
                Mx.GetValueOrDefault(
                    domain,
                    MailServerDnsResponse<MailServerMxHost>.NoData()));
        }

        public ValueTask<MailServerDnsResponse<string>> QueryMailServerCnameAsync(
            string domain,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add($"cname:{domain}");
            return ValueTask.FromResult(
                Cnames.GetValueOrDefault(
                    domain,
                    MailServerDnsResponse<string>.NoData()));
        }

        public ValueTask<MailServerDnsResponse<IPAddress>> QueryMailServerAddressesAsync(
            string domain,
            AddressFamily addressFamily,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(
                $"{(addressFamily == AddressFamily.InterNetwork ? "a" : "aaaa")}:{domain}");
            return ValueTask.FromResult(
                _addresses.GetValueOrDefault(
                    AddressKey(domain, addressFamily),
                    MailServerDnsResponse<IPAddress>.NoData()));
        }

        public void SetAddresses(
            string domain,
            AddressFamily addressFamily,
            params IPAddress[] addresses) =>
            SetAddressStatus(
                domain,
                addressFamily,
                MailServerDnsResponse<IPAddress>.Success(addresses));

        public void SetAddressStatus(
            string domain,
            AddressFamily addressFamily,
            MailServerDnsResponse<IPAddress> response) =>
            _addresses[AddressKey(domain, addressFamily)] = response;

        private static string AddressKey(string domain, AddressFamily addressFamily) =>
            $"{(int)addressFamily}:{domain}";
    }
}
