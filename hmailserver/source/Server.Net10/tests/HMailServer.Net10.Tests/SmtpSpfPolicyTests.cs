using System.Net;
using System.Net.Sockets;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpSpfPolicyTests
{
    [TestMethod]
    public async Task CheckAsync_SkipsWhenDisabledAuthenticatedOrSpamScanDisabled()
    {
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 -all");
        var disabled = new SmtpSpfPolicy(
            new SpfEvaluator(resolver),
            new SmtpSpfPolicyOptions { Enabled = false });
        var authenticated = new SmtpSpfPolicy(
            new SpfEvaluator(resolver),
            new SmtpSpfPolicyOptions { Enabled = true });

        var disabledResult = await disabled.CheckAsync(CreateRequest(), CancellationToken.None);
        var authenticatedResult = await authenticated.CheckAsync(
            CreateRequest() with { IsAuthenticated = true },
            CancellationToken.None);
        var spamDisabledResult = await authenticated.CheckAsync(
            CreateRequest() with { EnableSpamScan = false },
            CancellationToken.None);

        Assert.IsFalse(disabledResult.Evaluated);
        Assert.IsFalse(authenticatedResult.Evaluated);
        Assert.IsFalse(spamDisabledResult.Evaluated);
        Assert.AreEqual(0, resolver.Queries.Count);
    }

    [TestMethod]
    public async Task CheckAsync_MapsSpfFailToSpamResultWithoutRejecting()
    {
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 -all");
        var policy = new SmtpSpfPolicy(
            new SpfEvaluator(resolver),
            new SmtpSpfPolicyOptions
            {
                Enabled = true,
                FailScore = 7
            });

        var result = await policy.CheckAsync(CreateRequest(), CancellationToken.None);

        Assert.IsTrue(result.Evaluated);
        Assert.AreEqual(SmtpSpfPolicyStatus.Fail, result.Status);
        Assert.IsTrue(result.MarkAsSpam);
        Assert.IsFalse(result.Passed);
        Assert.AreEqual(7, result.Score);
        Assert.AreEqual("example.test", result.Domain);
        Assert.AreEqual("sender@example.test", result.Sender);
        Assert.AreEqual("-all", result.MatchedMechanism);
    }

    [TestMethod]
    public async Task CheckAsync_PreservesPassForLaterGreylistingBypassWithoutMarkingSpam()
    {
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", "v=spf1 +all");
        var policy = new SmtpSpfPolicy(
            new SpfEvaluator(resolver),
            new SmtpSpfPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(CreateRequest(), CancellationToken.None);

        Assert.IsTrue(result.Evaluated);
        Assert.AreEqual(SmtpSpfPolicyStatus.Pass, result.Status);
        Assert.IsFalse(result.MarkAsSpam);
        Assert.IsTrue(result.Passed);
        Assert.AreEqual(0, result.Score);
    }

    [TestMethod]
    [DataRow("v=spf1", SmtpSpfPolicyStatus.Neutral)]
    [DataRow("v=spf1 ~all", SmtpSpfPolicyStatus.SoftFail)]
    [DataRow("v=spf1 include:missing.example.test", SmtpSpfPolicyStatus.PermError)]
    public async Task CheckAsync_DoesNotMarkNonFailResultsAsSpam(
        string record,
        SmtpSpfPolicyStatus expectedStatus)
    {
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("example.test", record);
        var policy = new SmtpSpfPolicy(
            new SpfEvaluator(resolver),
            new SmtpSpfPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(CreateRequest(), CancellationToken.None);

        Assert.AreEqual(expectedStatus, result.Status);
        Assert.IsFalse(result.MarkAsSpam);
        Assert.IsFalse(result.Passed);
        Assert.AreEqual(0, result.Score);
    }

    [TestMethod]
    public async Task CheckAsync_UsesHeloIdentityForNullReversePath()
    {
        var resolver = new FakeSpfDnsResolver()
            .AddTxt("helo.example.test", "v=spf1 +all");
        var policy = new SmtpSpfPolicy(
            new SpfEvaluator(resolver),
            new SmtpSpfPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(
            CreateRequest() with
            {
                MailFrom = "<>",
                HeloHost = "helo.example.test"
            },
            CancellationToken.None);

        Assert.AreEqual(SmtpSpfPolicyStatus.Pass, result.Status);
        Assert.AreEqual("helo.example.test", result.Domain);
        Assert.AreEqual("postmaster@helo.example.test", result.Sender);
        CollectionAssert.Contains(resolver.Queries.ToArray(), "TXT:helo.example.test");
    }

    [TestMethod]
    public async Task CheckAsync_FailsOpenOnDnsTemporaryErrors()
    {
        var resolver = new FakeSpfDnsResolver()
            .SetTxtResponse("example.test", SpfDnsResponse<string>.TemporaryError());
        var policy = new SmtpSpfPolicy(
            new SpfEvaluator(resolver),
            new SmtpSpfPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(CreateRequest(), CancellationToken.None);

        Assert.AreEqual(SmtpSpfPolicyStatus.TempError, result.Status);
        Assert.IsFalse(result.MarkAsSpam);
        Assert.IsFalse(result.Passed);
    }

    [TestMethod]
    public async Task CheckAsync_SkipsMalformedClientOrSenderIdentity()
    {
        var resolver = new FakeSpfDnsResolver();
        var policy = new SmtpSpfPolicy(
            new SpfEvaluator(resolver),
            new SmtpSpfPolicyOptions { Enabled = true });

        var malformedIp = await policy.CheckAsync(
            CreateRequest() with { ClientIPAddress = "not-an-ip" },
            CancellationToken.None);
        var anyAddress = await policy.CheckAsync(
            CreateRequest() with { ClientIPAddress = "0.0.0.0" },
            CancellationToken.None);
        var malformedSender = await policy.CheckAsync(
            CreateRequest() with { MailFrom = "sender@[192.0.2.10]" },
            CancellationToken.None);

        Assert.IsFalse(malformedIp.Evaluated);
        Assert.IsFalse(anyAddress.Evaluated);
        Assert.IsFalse(malformedSender.Evaluated);
        Assert.AreEqual(0, resolver.Queries.Count);
    }

    private static SmtpReceiveRequest CreateRequest() =>
        new(
            HeloHost: "mail.example.test",
            IsExtendedSmtp: true,
            MailFrom: "sender@example.test",
            Recipients:
            [
                new SmtpResolvedRecipient(
                    "recipient@example.test",
                    "recipient@example.test",
                    LocalAccountId: 0,
                    IsLocal: false)
            ],
            DeclaredSize: null,
            MessageData: "Subject: Test\r\n\r\nBody\r\n"u8.ToArray(),
            ReceivedUtc: DateTimeOffset.UtcNow,
            ClientIPAddress: "192.0.2.5");

    private sealed class FakeSpfDnsResolver : ISpfDnsResolver
    {
        private readonly Dictionary<string, SpfDnsResponse<string>> _txt =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> Queries { get; } = [];

        public FakeSpfDnsResolver AddTxt(string domain, params string[] records) =>
            SetTxtResponse(domain, SpfDnsResponse<string>.Success(records));

        public FakeSpfDnsResolver SetTxtResponse(string domain, SpfDnsResponse<string> response)
        {
            _txt[Normalize(domain)] = response;
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

        private static string Normalize(string domain) =>
            domain.Trim().TrimEnd('.').ToLowerInvariant();
    }
}
