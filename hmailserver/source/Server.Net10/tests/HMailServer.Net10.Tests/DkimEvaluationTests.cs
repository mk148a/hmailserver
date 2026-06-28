using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DkimEvaluationTests
{
    [TestMethod]
    public void ResultModel_PreservesLegacyDkimResults()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                DkimResult.Neutral,
                DkimResult.Pass,
                DkimResult.TempFail,
                DkimResult.PermFail
            },
            Enum.GetValues<DkimResult>());

        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3 },
            Enum.GetValues<DkimResult>().Select(static result => (int)result).ToArray());
    }

    [TestMethod]
    public void TryParse_ExtractsRequiredTagsAndDefaultCanonicalization()
    {
        const string headerValue =
            "v=1; a=rsa-sha256; d=Example.TEST.; s=Mail; h= From : Subject : To ; " +
            "bh= YWJjZA== ; b= ZGVmZw== ";

        var parsed = DkimSignatureParser.TryParse(
            headerValue,
            out var signature,
            out var diagnostic);

        Assert.IsTrue(parsed, diagnostic);
        Assert.IsNotNull(signature);
        Assert.AreEqual("1", signature.Version);
        Assert.AreEqual("rsa-sha256", signature.Algorithm);
        Assert.AreEqual("example.test", signature.Domain);
        Assert.AreEqual("mail", signature.Selector);
        CollectionAssert.AreEqual(new[] { "From", "Subject", "To" }, signature.SignedHeaders.ToArray());
        Assert.AreEqual("YWJjZA==", signature.BodyHash);
        Assert.AreEqual("ZGVmZw==", signature.Signature);
        Assert.AreEqual(DkimCanonicalizationMethod.Simple, signature.HeaderCanonicalization);
        Assert.AreEqual(DkimCanonicalizationMethod.Simple, signature.BodyCanonicalization);
        Assert.AreEqual("dns/txt", signature.QueryMethod);
    }

    [TestMethod]
    public void TryParse_ReadsRelaxedSimpleCanonicalizationBodyLengthAndIdentity()
    {
        const string headerValue =
            "DKIM-Signature: v=1; a=rsa-sha1; c=relaxed/simple; q=dns/txt; " +
            "d=example.test; s=s1; i=local=2Dpart@sub.example.test; " +
            "h=from:date; l=123; bh=abc; b=def";

        var parsed = DkimSignatureParser.TryParse(
            headerValue,
            out var signature,
            out var diagnostic);

        Assert.IsTrue(parsed, diagnostic);
        Assert.IsNotNull(signature);
        Assert.AreEqual(DkimCanonicalizationMethod.Relaxed, signature.HeaderCanonicalization);
        Assert.AreEqual(DkimCanonicalizationMethod.Simple, signature.BodyCanonicalization);
        Assert.AreEqual("local-part@sub.example.test", signature.Identity);
        Assert.AreEqual(123, signature.BodyLength);
    }

    [TestMethod]
    [DataRow("v=1; a=rsa-sha256; d=example.test; s=s1; h=subject; bh=abc; b=def")]
    [DataRow("v=1; a=ed25519-sha256; d=example.test; s=s1; h=from; bh=abc; b=def")]
    [DataRow("v=1; a=rsa-sha256; d=example.test; s=s1; h=from; bh=abc")]
    [DataRow("v=1; a=rsa-sha256; d=example.test; s=s1; h=from; bh=abc; b=one; b=two")]
    [DataRow("v=1; a=rsa-sha256; c=relaxed/other; d=example.test; s=s1; h=from; bh=abc; b=def")]
    [DataRow("v=1; a=rsa-sha256; d=example.test; s=s1; i=user@other.test; h=from; bh=abc; b=def")]
    public void TryParse_RejectsInvalidSignatureFields(string headerValue)
    {
        var parsed = DkimSignatureParser.TryParse(
            headerValue,
            out var signature,
            out var diagnostic);

        Assert.IsFalse(parsed);
        Assert.IsNull(signature);
        Assert.AreNotEqual(string.Empty, diagnostic);
    }

    [TestMethod]
    public void CanonicalizeBody_AppliesSimpleRules()
    {
        var canonicalized = DkimCanonicalizer.CanonicalizeBody(
            "Line 1\r\nLine 2\r\n\r\n\r\n",
            DkimCanonicalizationMethod.Simple);

        Assert.AreEqual("Line 1\r\nLine 2\r\n", canonicalized);
        Assert.AreEqual(
            "\r\n",
            DkimCanonicalizer.CanonicalizeBody(string.Empty, DkimCanonicalizationMethod.Simple));
    }

    [TestMethod]
    public void CanonicalizeBody_AppliesRelaxedRules()
    {
        var canonicalized = DkimCanonicalizer.CanonicalizeBody(
            " A\t  B \r\nC\t \r\n\r\n",
            DkimCanonicalizationMethod.Relaxed);

        Assert.AreEqual(" A B\r\nC\r\n", canonicalized);
        Assert.AreEqual(
            string.Empty,
            DkimCanonicalizer.CanonicalizeBody("\r\n\r\n", DkimCanonicalizationMethod.Relaxed));
    }

    [TestMethod]
    public void CanonicalizeHeaderLine_AppliesRelaxedRules()
    {
        var canonicalized = DkimCanonicalizer.CanonicalizeHeaderLine(
            " Subject ",
            "  Test\r\n\t folded\t value \t",
            DkimCanonicalizationMethod.Relaxed);

        Assert.AreEqual("subject:Test folded value", canonicalized);
    }

    [TestMethod]
    public void CanonicalizeHeaders_SelectsSignedHeadersFromBottomAndEmptiesSignatureValue()
    {
        const string headerBlock =
            "From: first@example.test\r\n" +
            "Subject:  Test\r\n" +
            "From: final@example.test\r\n" +
            "X-Other: value\r\n";
        const string signatureValue =
            "v=1; a=rsa-sha256; d=example.test; s=s1; h=from:subject; " +
            "bh=abc; b=signature-data; q=dns/txt";

        var canonicalized = DkimCanonicalizer.CanonicalizeHeaders(
            headerBlock,
            "DKIM-Signature",
            signatureValue,
            new[] { "from", "subject" },
            DkimCanonicalizationMethod.Relaxed,
            out var fieldList);

        Assert.AreEqual("from:subject", fieldList);
        Assert.AreEqual(
            "from:final@example.test\r\n" +
            "subject:Test\r\n" +
            "dkim-signature:v=1; a=rsa-sha256; d=example.test; s=s1; h=from:subject; bh=abc; b=; q=dns/txt",
            canonicalized);
    }
}
