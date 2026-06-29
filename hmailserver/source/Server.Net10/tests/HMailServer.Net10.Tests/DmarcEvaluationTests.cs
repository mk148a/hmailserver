using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DmarcEvaluationTests
{
    [TestMethod]
    public void ResultModel_PreservesDmarcResults()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                DmarcResult.None,
                DmarcResult.Pass,
                DmarcResult.Fail,
                DmarcResult.TempError,
                DmarcResult.PermError
            },
            Enum.GetValues<DmarcResult>());
    }

    [TestMethod]
    public async Task EvaluateAsync_QueriesHeaderFromDmarcNameAndReturnsNoneWhenNoRecordExists()
    {
        var resolver = new FakeDmarcTxtResolver();

        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest("Example.TEST."),
            resolver);

        Assert.AreEqual(DmarcResult.None, result.Result);
        Assert.AreEqual(DmarcPolicy.None, result.AppliedPolicy);
        Assert.IsNull(result.Record);
        CollectionAssert.AreEqual(new[] { "_dmarc.example.test" }, resolver.Queries.ToArray());
    }

    [TestMethod]
    public async Task EvaluateAsync_ParsesRecordDefaultsAndFailsWithConfiguredPolicy()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=reject; rua=mailto:dmarc@example.test");

        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest("example.test"),
            resolver);

        Assert.AreEqual(DmarcResult.Fail, result.Result);
        Assert.AreEqual(DmarcPolicy.Reject, result.AppliedPolicy);
        Assert.IsNotNull(result.Record);
        Assert.AreEqual("example.test", result.Record.Domain);
        Assert.AreEqual("_dmarc.example.test", result.Record.QueryName);
        Assert.AreEqual(DmarcAlignmentMode.Relaxed, result.Record.SpfAlignment);
        Assert.AreEqual(DmarcAlignmentMode.Relaxed, result.Record.DkimAlignment);
        Assert.AreEqual(100, result.Record.Percentage);
        Assert.AreEqual("mailto:dmarc@example.test", result.Record.Tags["rua"]);
    }

    [TestMethod]
    public async Task EvaluateAsync_PassesWhenSpfPassDomainAlignsRelaxed()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=reject");

        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest(
                "example.test",
                Spf: new DmarcSpfAuthenticationResult(true, "mail.example.test")),
            resolver);

        Assert.AreEqual(DmarcResult.Pass, result.Result);
        Assert.AreEqual(DmarcPolicy.None, result.AppliedPolicy);
        StringAssert.Contains(result.Diagnostic, "SPF");
    }

    [TestMethod]
    public async Task EvaluateAsync_PassesWhenDkimPassDomainAlignsStrict()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=quarantine; adkim=s");

        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest(
                "example.test",
                Dkim:
                [
                    new DmarcDkimAuthenticationResult(false, "other.test"),
                    new DmarcDkimAuthenticationResult(true, "example.test")
                ]),
            resolver);

        Assert.AreEqual(DmarcResult.Pass, result.Result);
        Assert.AreEqual(DmarcPolicy.None, result.AppliedPolicy);
        StringAssert.Contains(result.Diagnostic, "DKIM");
    }

    [TestMethod]
    public async Task EvaluateAsync_FailsWhenPassingAuthenticationDoesNotAlign()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=quarantine; aspf=s; adkim=s");

        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest(
                "example.test",
                Spf: new DmarcSpfAuthenticationResult(true, "mail.example.test"),
                Dkim: [new DmarcDkimAuthenticationResult(true, "signer.example.test")]),
            resolver);

        Assert.AreEqual(DmarcResult.Fail, result.Result);
        Assert.AreEqual(DmarcPolicy.Quarantine, result.AppliedPolicy);
    }

    [TestMethod]
    public async Task EvaluateAsync_UsesOrganizationalDomainFallbackAndSubdomainPolicy()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=none; sp=reject; pct=50");

        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest(
                "mail.example.test",
                OrganizationalDomain: "example.test"),
            resolver);

        Assert.AreEqual(DmarcResult.Fail, result.Result);
        Assert.AreEqual(DmarcPolicy.Reject, result.AppliedPolicy);
        Assert.IsNotNull(result.Record);
        Assert.AreEqual("example.test", result.Record.Domain);
        Assert.AreEqual(50, result.Record.Percentage);
        CollectionAssert.AreEqual(
            new[] { "_dmarc.mail.example.test", "_dmarc.example.test" },
            resolver.Queries.ToArray());
    }

    [TestMethod]
    public async Task EvaluateAsync_UsesOrganizationalDomainForRelaxedSiblingAlignment()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=reject");

        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest(
                "mail.example.test",
                Spf: new DmarcSpfAuthenticationResult(true, "bounce.example.test"),
                OrganizationalDomain: "example.test"),
            resolver);

        Assert.AreEqual(DmarcResult.Pass, result.Result);
    }

    [TestMethod]
    public async Task EvaluateAsync_ReturnsTempErrorForTemporaryDnsFailure()
    {
        var resolver = new FakeDmarcTxtResolver()
            .SetResponse("_dmarc.example.test", DmarcTxtResponse.TemporaryError());

        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest("example.test"),
            resolver);

        Assert.AreEqual(DmarcResult.TempError, result.Result);
        Assert.AreEqual(DmarcPolicy.None, result.AppliedPolicy);
        Assert.IsNull(result.Record);
    }

    [TestMethod]
    public async Task EvaluateAsync_ReturnsPermErrorForMalformedRecord()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt("_dmarc.example.test", "v=DMARC1; p=invalid");

        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest("example.test"),
            resolver);

        Assert.AreEqual(DmarcResult.PermError, result.Result);
        StringAssert.Contains(result.Diagnostic, "p tag");
        Assert.IsNull(result.Record);
    }

    [TestMethod]
    public async Task EvaluateAsync_ReturnsPermErrorForDuplicateDmarcRecords()
    {
        var resolver = new FakeDmarcTxtResolver()
            .AddTxt(
                "_dmarc.example.test",
                "v=DMARC1; p=none",
                "v=DMARC1; p=reject");

        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest("example.test"),
            resolver);

        Assert.AreEqual(DmarcResult.PermError, result.Result);
        StringAssert.Contains(result.Diagnostic, "multiple");
    }

    [TestMethod]
    public async Task EvaluateAsync_ReturnsPermErrorForInvalidOrganizationalDomain()
    {
        var result = await DmarcEvaluator.EvaluateAsync(
            new DmarcEvaluationRequest(
                "mail.example.test",
                OrganizationalDomain: "other.test"),
            new FakeDmarcTxtResolver());

        Assert.AreEqual(DmarcResult.PermError, result.Result);
    }

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
}
