using System.Security.Cryptography;
using System.Text;
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

    [TestMethod]
    public void VerifyBodyHash_ReturnsNeutralForMatchingSimpleSha256BodyHash()
    {
        var signature = ParseSignature(
            "v=1; a=rsa-sha256; c=simple/simple; d=example.test; s=s1; h=from; " +
            "bh=frcCV1k9oG9oKj3dpUqdJg1PxRT2RSN/XKdLCPjaYaY=; b=def");

        var result = DkimBodyHashVerifier.VerifyBodyHash(
            string.Empty,
            signature);

        Assert.AreEqual(DkimResult.Neutral, result.Result);
        StringAssert.Contains(result.Diagnostic, "body hash verified");
    }

    [TestMethod]
    public void VerifyBodyHash_UsesRelaxedBodyCanonicalizationAndIgnoredBhWhitespace()
    {
        var signature = ParseSignature(
            "v=1; a=rsa-sha256; c=simple/relaxed; d=example.test; s=s1; h=from; " +
            "bh=47DEQpj8HBSa+/TImW+5 JCeuQeRkm5NMpJWZG3hSuFU=; b=def");

        var result = DkimBodyHashVerifier.VerifyBodyHash(
            "\r\n\r\n",
            signature);

        Assert.AreEqual(DkimResult.Neutral, result.Result);
    }

    [TestMethod]
    public void VerifyBodyHash_ReturnsPermFailForMismatchedBodyHash()
    {
        var signature = ParseSignature(
            "v=1; a=rsa-sha256; c=simple/simple; d=example.test; s=s1; h=from; " +
            "bh=frcCV1k9oG9oKj3dpUqdJg1PxRT2RSN/XKdLCPjaYaY=; b=def");

        var result = DkimBodyHashVerifier.VerifyBodyHash(
            "tampered\r\n",
            signature);

        Assert.AreEqual(DkimResult.PermFail, result.Result);
        StringAssert.Contains(result.Diagnostic, "does not match");
    }

    [TestMethod]
    public void VerifyBodyHash_AppliesBodyLengthBeforeSha1Hashing()
    {
        var signature = ParseSignature(
            "v=1; a=rsa-sha1; c=simple/simple; d=example.test; s=s1; h=from; l=5; " +
            "bh=qvTGHdzF6KLavt4PO0gs2a6pQ00=; b=def");

        var result = DkimBodyHashVerifier.VerifyBodyHash(
            "hello world\r\n",
            signature);

        Assert.AreEqual(DkimResult.Neutral, result.Result);
    }

    [TestMethod]
    public void VerifyBodyHash_ReturnsPermFailWhenBodyLengthExceedsCanonicalizedBody()
    {
        var signature = ParseSignature(
            "v=1; a=rsa-sha256; c=simple/simple; d=example.test; s=s1; h=from; l=8; " +
            "bh=LPJNul+wow4m6DsqxbninhsWHlwfp0JecwQzYpOLmCQ=; b=def");

        var result = DkimBodyHashVerifier.VerifyBodyHash(
            "short",
            signature);

        Assert.AreEqual(DkimResult.PermFail, result.Result);
        StringAssert.Contains(result.Diagnostic, "exceeds");
    }

    [TestMethod]
    public void Verify_ReturnsPassWhenBodyHashAndHeaderSignaturePass()
    {
        var fixture = CreateSignedFixture();

        var result = DkimSignatureVerifier.Verify(
            fixture.HeaderBlock,
            fixture.Body,
            fixture.SignatureHeaderValue,
            fixture.Signature,
            fixture.PublicKeyBase64);

        Assert.AreEqual(DkimResult.Pass, result.Result);
        StringAssert.Contains(result.Diagnostic, "header signature verified");
    }

    [TestMethod]
    public void Verify_ReturnsPermFailWhenSignedHeaderChanges()
    {
        var fixture = CreateSignedFixture();
        var tamperedHeaderBlock = fixture.HeaderBlock.Replace(
            "Subject: Test",
            "Subject: Tampered",
            StringComparison.Ordinal);

        var result = DkimSignatureVerifier.Verify(
            tamperedHeaderBlock,
            fixture.Body,
            fixture.SignatureHeaderValue,
            fixture.Signature,
            fixture.PublicKeyBase64);

        Assert.AreEqual(DkimResult.PermFail, result.Result);
        StringAssert.Contains(result.Diagnostic, "signature does not match");
    }

    [TestMethod]
    public void Verify_ReturnsPermFailWhenBodyHashFailsBeforeHeaderSignature()
    {
        var fixture = CreateSignedFixture();

        var result = DkimSignatureVerifier.Verify(
            fixture.HeaderBlock,
            "tampered\r\n",
            fixture.SignatureHeaderValue,
            fixture.Signature,
            fixture.PublicKeyBase64);

        Assert.AreEqual(DkimResult.PermFail, result.Result);
        StringAssert.Contains(result.Diagnostic, "body hash");
    }

    [TestMethod]
    public void Verify_SupportsLegacyRsaSha1HeaderSignatures()
    {
        var fixture = CreateSignedFixture("rsa-sha1");

        var result = DkimSignatureVerifier.Verify(
            fixture.HeaderBlock,
            fixture.Body,
            fixture.SignatureHeaderValue,
            fixture.Signature,
            fixture.PublicKeyBase64);

        Assert.AreEqual(DkimResult.Pass, result.Result);
    }

    [TestMethod]
    public void Verify_ReturnsPermFailForInvalidPublicKey()
    {
        var fixture = CreateSignedFixture();

        var result = DkimSignatureVerifier.Verify(
            fixture.HeaderBlock,
            fixture.Body,
            fixture.SignatureHeaderValue,
            fixture.Signature,
            "not-base64");

        Assert.AreEqual(DkimResult.PermFail, result.Result);
        StringAssert.Contains(result.Diagnostic, "public key");
    }

    private static DkimSignature ParseSignature(string value)
    {
        var parsed = DkimSignatureParser.TryParse(
            value,
            out var signature,
            out var diagnostic);

        Assert.IsTrue(parsed, diagnostic);
        Assert.IsNotNull(signature);
        return signature;
    }

    private static SignedDkimFixture CreateSignedFixture(
        string algorithm = "rsa-sha256",
        string body = "",
        DkimCanonicalizationMethod headerCanonicalization = DkimCanonicalizationMethod.Relaxed,
        DkimCanonicalizationMethod bodyCanonicalization = DkimCanonicalizationMethod.Simple)
    {
        const string headerBlock =
            "From: sender@example.test\r\n" +
            "Subject: Test\r\n";
        var bodyHash = ComputeBodyHash(body, bodyCanonicalization, algorithm);
        var unsignedSignatureHeaderValue =
            $"v=1; a={algorithm}; c={ToDkimCanonicalizationName(headerCanonicalization)}/{ToDkimCanonicalizationName(bodyCanonicalization)}; " +
            $"d=example.test; s=s1; h=from:subject; bh={bodyHash}; b=";
        var signedHeaders = new[] { "from", "subject" };
        using var rsa = RSA.Create(2048);
        var canonicalizedHeader = DkimCanonicalizer.CanonicalizeHeaders(
            headerBlock,
            "DKIM-Signature",
            unsignedSignatureHeaderValue,
            signedHeaders,
            headerCanonicalization,
            out _);
        var headerBytes = Encoding.Latin1.GetBytes(canonicalizedHeader);
        var signatureValue = Convert.ToBase64String(rsa.SignData(
            headerBytes,
            ToHashAlgorithmName(algorithm),
            RSASignaturePadding.Pkcs1));
        var signatureHeaderValue = unsignedSignatureHeaderValue + signatureValue;
        var signature = ParseSignature(signatureHeaderValue);
        var publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

        return new SignedDkimFixture(
            headerBlock,
            body,
            signatureHeaderValue,
            signature,
            publicKeyBase64);
    }

    private static string ComputeBodyHash(
        string body,
        DkimCanonicalizationMethod bodyCanonicalization,
        string algorithm)
    {
        var canonicalizedBody = DkimCanonicalizer.CanonicalizeBody(body, bodyCanonicalization);
        var bodyBytes = Encoding.Latin1.GetBytes(canonicalizedBody);
        var hash = algorithm switch
        {
            "rsa-sha1" => SHA1.HashData(bodyBytes),
            "rsa-sha256" => SHA256.HashData(bodyBytes),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported DKIM hash algorithm.")
        };

        return Convert.ToBase64String(hash);
    }

    private static HashAlgorithmName ToHashAlgorithmName(string algorithm) =>
        algorithm switch
        {
            "rsa-sha1" => HashAlgorithmName.SHA1,
            "rsa-sha256" => HashAlgorithmName.SHA256,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported DKIM hash algorithm.")
        };

    private static string ToDkimCanonicalizationName(DkimCanonicalizationMethod canonicalization) =>
        canonicalization switch
        {
            DkimCanonicalizationMethod.Simple => "simple",
            DkimCanonicalizationMethod.Relaxed => "relaxed",
            _ => throw new ArgumentOutOfRangeException(nameof(canonicalization), canonicalization, "Unsupported DKIM canonicalization.")
        };

    private sealed record SignedDkimFixture(
        string HeaderBlock,
        string Body,
        string SignatureHeaderValue,
        DkimSignature Signature,
        string PublicKeyBase64);
}
