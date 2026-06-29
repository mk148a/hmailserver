using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpDmarcPolicyTests
{
    [TestMethod]
    public async Task CheckAsync_SkipsWhenDisabledAuthenticatedOrSpamScanDisabled()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=reject");
        var disabled = new SmtpDmarcPolicy(
            resolver,
            new SmtpDmarcPolicyOptions { Enabled = false });
        var enabled = new SmtpDmarcPolicy(
            resolver,
            new SmtpDmarcPolicyOptions { Enabled = true });

        var disabledResult = await disabled.CheckAsync(
            CreateRequest(),
            SmtpSpfPolicyResult.Skipped,
            SmtpDkimPolicyResult.Skipped,
            CancellationToken.None);
        var authenticatedResult = await enabled.CheckAsync(
            CreateRequest() with { IsAuthenticated = true },
            SmtpSpfPolicyResult.Skipped,
            SmtpDkimPolicyResult.Skipped,
            CancellationToken.None);
        var spamDisabledResult = await enabled.CheckAsync(
            CreateRequest() with { EnableSpamScan = false },
            SmtpSpfPolicyResult.Skipped,
            SmtpDkimPolicyResult.Skipped,
            CancellationToken.None);

        Assert.IsFalse(disabledResult.Evaluated);
        Assert.IsFalse(authenticatedResult.Evaluated);
        Assert.IsFalse(spamDisabledResult.Evaluated);
        Assert.AreEqual(0, resolver.Queries.Count);
    }

    [TestMethod]
    public async Task CheckAsync_MapsAlignedSpfPassToDmarcPassWithoutMarkingSpam()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=reject");
        var policy = new SmtpDmarcPolicy(
            resolver,
            new SmtpDmarcPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(
            CreateRequest(),
            CreateSpfPolicyResult(SmtpSpfPolicyStatus.Pass, "mail.example.test"),
            SmtpDkimPolicyResult.Skipped,
            CancellationToken.None);

        Assert.IsTrue(result.Evaluated);
        Assert.AreEqual(SmtpDmarcPolicyStatus.Pass, result.Status);
        Assert.IsTrue(result.Passed);
        Assert.IsFalse(result.MarkAsSpam);
        Assert.AreEqual(0, result.Score);
        Assert.AreEqual("example.test", result.HeaderFromDomain);
        CollectionAssert.Contains(resolver.Queries.ToArray(), "_dmarc.example.test");
    }

    [TestMethod]
    public async Task CheckAsync_MapsAlignedDkimPassToDmarcPassWithoutMarkingSpam()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=reject; adkim=s");
        var policy = new SmtpDmarcPolicy(
            resolver,
            new SmtpDmarcPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(
            CreateRequest(),
            SmtpSpfPolicyResult.Skipped,
            SmtpDkimPolicyResult.FromEvaluation(
                SmtpDkimPolicyStatus.Pass,
                failureScore: 5,
                diagnostic: "DKIM pass.",
                passingDomains: ["example.test"]),
            CancellationToken.None);

        Assert.AreEqual(SmtpDmarcPolicyStatus.Pass, result.Status);
        Assert.IsTrue(result.Passed);
        Assert.IsFalse(result.MarkAsSpam);
    }

    [TestMethod]
    public async Task CheckAsync_MapsPolicyFailureWithoutRejectingOrMarkingSpamByDefault()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=reject");
        var policy = new SmtpDmarcPolicy(
            resolver,
            new SmtpDmarcPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(
            CreateRequest(),
            SmtpSpfPolicyResult.Skipped,
            SmtpDkimPolicyResult.Skipped,
            CancellationToken.None);

        Assert.AreEqual(SmtpDmarcPolicyStatus.Fail, result.Status);
        Assert.AreEqual(SmtpDmarcAppliedPolicy.Reject, result.AppliedPolicy);
        Assert.IsFalse(result.Passed);
        Assert.IsFalse(result.MarkAsSpam);
        Assert.AreEqual(0, result.Score);
    }

    [TestMethod]
    public async Task CheckAsync_CanMarkPolicyFailuresAsSpamWhenExplicitlyConfigured()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=quarantine");
        var policy = new SmtpDmarcPolicy(
            resolver,
            new SmtpDmarcPolicyOptions
            {
                Enabled = true,
                MarkPolicyFailuresAsSpam = true,
                FailureScore = 9
            });

        var result = await policy.CheckAsync(
            CreateRequest(),
            SmtpSpfPolicyResult.Skipped,
            SmtpDkimPolicyResult.Skipped,
            CancellationToken.None);

        Assert.AreEqual(SmtpDmarcPolicyStatus.Fail, result.Status);
        Assert.AreEqual(SmtpDmarcAppliedPolicy.Quarantine, result.AppliedPolicy);
        Assert.IsTrue(result.MarkAsSpam);
        Assert.AreEqual(9, result.Score);
    }

    [TestMethod]
    public async Task CheckAsync_FailsOpenForTemporaryDnsFailures()
    {
        var resolver = new FakeDmarcTxtResolver()
            .SetResponse("_dmarc.example.test", DmarcTxtResponse.TemporaryError());
        var policy = new SmtpDmarcPolicy(
            resolver,
            new SmtpDmarcPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(
            CreateRequest(),
            SmtpSpfPolicyResult.Skipped,
            SmtpDkimPolicyResult.Skipped,
            CancellationToken.None);

        Assert.AreEqual(SmtpDmarcPolicyStatus.TempError, result.Status);
        Assert.IsFalse(result.MarkAsSpam);
    }

    [TestMethod]
    public async Task CheckAsync_FailsOpenForMalformedHeaderFromWithoutDnsLookup()
    {
        var resolver = new FakeDmarcTxtResolver();
        var policy = new SmtpDmarcPolicy(
            resolver,
            new SmtpDmarcPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(
            CreateRequest("Subject: No From\r\n\r\nBody\r\n"),
            SmtpSpfPolicyResult.Skipped,
            SmtpDkimPolicyResult.Skipped,
            CancellationToken.None);

        Assert.AreEqual(SmtpDmarcPolicyStatus.PermError, result.Status);
        Assert.IsFalse(result.MarkAsSpam);
        Assert.AreEqual(0, resolver.Queries.Count);
    }

    [TestMethod]
    public async Task CheckAsync_FailsOpenWhenResolverThrows()
    {
        var policy = new SmtpDmarcPolicy(
            new ThrowingDmarcTxtResolver(),
            new SmtpDmarcPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(
            CreateRequest(),
            SmtpSpfPolicyResult.Skipped,
            SmtpDkimPolicyResult.Skipped,
            CancellationToken.None);

        Assert.AreEqual(SmtpDmarcPolicyStatus.TempError, result.Status);
        Assert.IsFalse(result.MarkAsSpam);
        StringAssert.Contains(result.Diagnostic, "failed open");
    }

    private static SmtpReceiveRequest CreateRequest(string? message = null) =>
        new(
            HeloHost: "client.example",
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
            MessageData: Encoding.Latin1.GetBytes(
                message ?? "From: Sender <sender@example.test>\r\nSubject: DMARC\r\n\r\nBody\r\n"),
            ReceivedUtc: DateTimeOffset.UtcNow,
            ClientIPAddress: "192.0.2.5");

    private static SmtpSpfPolicyResult CreateSpfPolicyResult(
        SmtpSpfPolicyStatus status,
        string domain) =>
        SmtpSpfPolicyResult.FromEvaluation(
            status,
            failScore: 3,
            domain,
            sender: "sender@" + domain,
            heloDomain: "client.example",
            matchedMechanism: status == SmtpSpfPolicyStatus.Pass ? "+all" : null,
            diagnostic: status.ToString());

    private sealed class FakeDmarcTxtResolver : IDmarcTxtResolver
    {
        private readonly Dictionary<string, DmarcTxtResponse> _responses = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Queries { get; } = [];

        public FakeDmarcTxtResolver AddTxt(string domain, params string[] records) =>
            SetResponse(domain, DmarcTxtResponse.Success(records));

        public FakeDmarcTxtResolver SetResponse(string domain, DmarcTxtResponse response)
        {
            _responses[Normalize(domain)] = response;
            return this;
        }

        public ValueTask<DmarcTxtResponse> QueryTxtAsync(
            string domain,
            CancellationToken cancellationToken)
        {
            var normalized = Normalize(domain);
            Queries.Add(normalized);
            return ValueTask.FromResult(_responses.TryGetValue(normalized, out var response)
                ? response
                : DmarcTxtResponse.NoData());
        }

        private static string Normalize(string value) =>
            value.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private sealed class ThrowingDmarcTxtResolver : IDmarcTxtResolver
    {
        public ValueTask<DmarcTxtResponse> QueryTxtAsync(
            string domain,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("DNS unavailable");
    }
}
