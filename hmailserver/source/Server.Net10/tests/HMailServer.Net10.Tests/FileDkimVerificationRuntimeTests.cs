using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class FileDkimVerificationRuntimeTests
{
    [TestMethod]
    public void Verify_ReturnsNeutralWithoutSignature()
    {
        var path = WriteMessage("From: sender@example.test\r\nSubject: Test\r\n\r\nBody");
        var resolver = new RecordingResolver();

        try
        {
            var result = new FileDkimVerificationRuntime(resolver).Verify(path);

            Assert.AreEqual(DkimVerificationResult.Neutral, result);
            Assert.AreEqual(0, resolver.Queries.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Verify_UsesDnsResolverAndMapsTemporaryFailure()
    {
        const string message =
            "DKIM-Signature: v=1; a=rsa-sha256; c=relaxed/simple; d=example.test; s=s1; " +
            "h=from; bh=YWJj; b=YWJj\r\n" +
            "From: sender@example.test\r\n\r\nBody";
        var path = WriteMessage(message);
        var resolver = new RecordingResolver(DkimTxtResponse.TemporaryError());

        try
        {
            var result = new FileDkimVerificationRuntime(resolver).Verify(path);

            Assert.AreEqual(DkimVerificationResult.TempFail, result);
            CollectionAssert.AreEqual(
                new[] { "s1._domainkey.example.test" },
                resolver.Queries);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Verify_ReturnsNeutralWithoutReadingDnsWhenFileExceedsLegacyBound()
    {
        var path = WriteMessage(new string('x', 33));
        var resolver = new RecordingResolver(DkimTxtResponse.TemporaryError());
        var runtime = new FileDkimVerificationRuntime(
            resolver,
            new FileDkimVerificationRuntimeOptions { MaximumMessageBytes = 32 });

        try
        {
            var result = runtime.Verify(path);

            Assert.AreEqual(DkimVerificationResult.Neutral, result);
            Assert.AreEqual(0, resolver.Queries.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Options_PreserveLegacyFiftyMegabyteLimit()
    {
        Assert.AreEqual(
            50 * 1024 * 1024,
            new FileDkimVerificationRuntimeOptions().MaximumMessageBytes);
    }

    private static string WriteMessage(string message)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hmailserver-dkim-{Guid.NewGuid():N}.eml");
        File.WriteAllBytes(path, System.Text.Encoding.Latin1.GetBytes(message));
        return path;
    }

    private sealed class RecordingResolver(DkimTxtResponse? response = null) : IDkimTxtResolver
    {
        public List<string> Queries { get; } = [];

        public ValueTask<DkimTxtResponse> QueryTxtAsync(
            string domain,
            CancellationToken cancellationToken)
        {
            Queries.Add(domain);
            return ValueTask.FromResult(response ?? DkimTxtResponse.NoData());
        }
    }
}
