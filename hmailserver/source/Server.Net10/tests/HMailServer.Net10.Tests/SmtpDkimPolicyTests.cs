using System.Security.Cryptography;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpDkimPolicyTests
{
    [TestMethod]
    public async Task CheckAsync_SkipsWhenDisabledAuthenticatedOrSpamScanDisabled()
    {
        var resolver = new FakeDkimTxtResolver();
        var disabled = new SmtpDkimPolicy(
            resolver,
            new SmtpDkimPolicyOptions { Enabled = false });
        var enabled = new SmtpDkimPolicy(
            resolver,
            new SmtpDkimPolicyOptions { Enabled = true });

        var disabledResult = await disabled.CheckAsync(CreateRequest(CreateDkimFailureMessage()), CancellationToken.None);
        var authenticatedResult = await enabled.CheckAsync(
            CreateRequest(CreateDkimFailureMessage()) with { IsAuthenticated = true },
            CancellationToken.None);
        var spamDisabledResult = await enabled.CheckAsync(
            CreateRequest(CreateDkimFailureMessage()) with { EnableSpamScan = false },
            CancellationToken.None);

        Assert.IsFalse(disabledResult.Evaluated);
        Assert.IsFalse(authenticatedResult.Evaluated);
        Assert.IsFalse(spamDisabledResult.Evaluated);
        Assert.AreEqual(0, resolver.Queries.Count);
    }

    [TestMethod]
    public async Task CheckAsync_MapsPassWithoutMarkingSpam()
    {
        var fixture = CreateSignedFixture();
        var resolver = new FakeDkimTxtResolver()
            .AddTxt(
                "s1._domainkey.example.test",
                $"v=DKIM1; h=sha256; p={fixture.PublicKeyBase64}");
        var policy = new SmtpDkimPolicy(
            resolver,
            new SmtpDkimPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(CreateRequest(fixture.RawMessage), CancellationToken.None);

        Assert.IsTrue(result.Evaluated);
        Assert.AreEqual(SmtpDkimPolicyStatus.Pass, result.Status);
        Assert.IsTrue(result.Passed);
        Assert.IsFalse(result.MarkAsSpam);
        Assert.AreEqual(0, result.Score);
        CollectionAssert.Contains(resolver.Queries.ToArray(), "s1._domainkey.example.test");
    }

    [TestMethod]
    public async Task CheckAsync_MapsPermFailToSpamResultWithoutRejecting()
    {
        var policy = new SmtpDkimPolicy(
            new FakeDkimTxtResolver(),
            new SmtpDkimPolicyOptions
            {
                Enabled = true,
                FailureScore = 7
            });

        var result = await policy.CheckAsync(CreateRequest(CreateDkimFailureMessage()), CancellationToken.None);

        Assert.IsTrue(result.Evaluated);
        Assert.AreEqual(SmtpDkimPolicyStatus.PermFail, result.Status);
        Assert.IsTrue(result.MarkAsSpam);
        Assert.IsFalse(result.Passed);
        Assert.AreEqual(7, result.Score);
        StringAssert.Contains(result.Diagnostic, "no key");
    }

    [TestMethod]
    public async Task CheckAsync_FailsOpenForNeutralAndTemporaryFailures()
    {
        var neutral = new SmtpDkimPolicy(
            new FakeDkimTxtResolver(),
            new SmtpDkimPolicyOptions { Enabled = true });
        var tempFail = new SmtpDkimPolicy(
            new FakeDkimTxtResolver()
                .SetResponse("s1._domainkey.example.test", DkimTxtResponse.TemporaryError()),
            new SmtpDkimPolicyOptions { Enabled = true });

        var neutralResult = await neutral.CheckAsync(
            CreateRequest("From: sender@example.test\r\nSubject: No DKIM\r\n\r\nBody\r\n"),
            CancellationToken.None);
        var tempFailResult = await tempFail.CheckAsync(
            CreateRequest(CreateDkimFailureMessage()),
            CancellationToken.None);

        Assert.AreEqual(SmtpDkimPolicyStatus.Neutral, neutralResult.Status);
        Assert.IsFalse(neutralResult.MarkAsSpam);
        Assert.IsFalse(neutralResult.Passed);
        Assert.AreEqual(SmtpDkimPolicyStatus.TempFail, tempFailResult.Status);
        Assert.IsFalse(tempFailResult.MarkAsSpam);
        Assert.IsFalse(tempFailResult.Passed);
    }

    [TestMethod]
    public async Task CheckAsync_FailsOpenWhenResolverThrows()
    {
        var policy = new SmtpDkimPolicy(
            new ThrowingDkimTxtResolver(),
            new SmtpDkimPolicyOptions { Enabled = true });

        var result = await policy.CheckAsync(CreateRequest(CreateDkimFailureMessage()), CancellationToken.None);

        Assert.AreEqual(SmtpDkimPolicyStatus.TempFail, result.Status);
        Assert.IsFalse(result.MarkAsSpam);
        Assert.IsFalse(result.Passed);
        StringAssert.Contains(result.Diagnostic, "failed open");
    }

    private static SmtpReceiveRequest CreateRequest(string message) =>
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
            MessageData: Encoding.Latin1.GetBytes(message),
            ReceivedUtc: DateTimeOffset.UtcNow,
            ClientIPAddress: "192.0.2.5");

    private static string CreateDkimFailureMessage() =>
        "From: sender@example.test\r\n" +
        "Subject: DKIM\r\n" +
        "DKIM-Signature: v=1; a=rsa-sha256; d=example.test; s=s1; h=from; bh=abc; b=def\r\n" +
        "\r\n" +
        "Body\r\n";

    private static SignedDkimFixture CreateSignedFixture()
    {
        const string headerBlock =
            "From: sender@example.test\r\n" +
            "Subject: Test\r\n";
        const string body = "";
        var bodyHash = ComputeBodyHash(body);
        var unsignedSignatureHeaderValue =
            "v=1; a=rsa-sha256; c=relaxed/simple; " +
            $"d=example.test; s=s1; h=from:subject; bh={bodyHash}; b=";
        var signedHeaders = new[] { "from", "subject" };
        using var rsa = RSA.Create(2048);
        var canonicalizedHeader = DkimCanonicalizer.CanonicalizeHeaders(
            headerBlock,
            "DKIM-Signature",
            unsignedSignatureHeaderValue,
            signedHeaders,
            DkimCanonicalizationMethod.Relaxed,
            out _);
        var signatureValue = Convert.ToBase64String(rsa.SignData(
            Encoding.Latin1.GetBytes(canonicalizedHeader),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
        var signatureHeaderValue = unsignedSignatureHeaderValue + signatureValue;
        var publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        var rawMessage =
            headerBlock +
            "DKIM-Signature: " + signatureHeaderValue + "\r\n" +
            "\r\n" +
            body;

        return new SignedDkimFixture(rawMessage, publicKeyBase64);
    }

    private static string ComputeBodyHash(string body)
    {
        var canonicalizedBody = DkimCanonicalizer.CanonicalizeBody(
            body,
            DkimCanonicalizationMethod.Simple);
        return Convert.ToBase64String(SHA256.HashData(Encoding.Latin1.GetBytes(canonicalizedBody)));
    }

    private sealed record SignedDkimFixture(
        string RawMessage,
        string PublicKeyBase64);

    private sealed class FakeDkimTxtResolver : IDkimTxtResolver
    {
        private readonly Dictionary<string, DkimTxtResponse> _responses = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Queries { get; } = [];

        public FakeDkimTxtResolver AddTxt(string domain, params string[] records) =>
            SetResponse(domain, DkimTxtResponse.Success(records));

        public FakeDkimTxtResolver SetResponse(string domain, DkimTxtResponse response)
        {
            _responses[Normalize(domain)] = response;
            return this;
        }

        public ValueTask<DkimTxtResponse> QueryTxtAsync(
            string domain,
            CancellationToken cancellationToken)
        {
            var normalized = Normalize(domain);
            Queries.Add(normalized);
            return ValueTask.FromResult(_responses.TryGetValue(normalized, out var response)
                ? response
                : DkimTxtResponse.NoData());
        }

        private static string Normalize(string value) =>
            value.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private sealed class ThrowingDkimTxtResolver : IDkimTxtResolver
    {
        public ValueTask<DkimTxtResponse> QueryTxtAsync(
            string domain,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("DNS unavailable");
    }
}
