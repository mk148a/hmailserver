using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SpamAssassinResponseValidatorTests
{
    [TestMethod]
    public void TryReadContentLength_AcceptsPositiveContentLength()
    {
        var ok = SpamAssassinResponseValidator.TryReadContentLength(
            "SPAMD/1.1 0 EX_OK\r\nContent-length: 42",
            out var contentLength);

        Assert.IsTrue(ok);
        Assert.AreEqual(42, contentLength);
    }

    [TestMethod]
    [DataRow("SPAMD/1.1 0 EX_OK")]
    [DataRow("SPAMD/1.1 0 EX_OK\r\nContent-length: -1")]
    [DataRow("SPAMD/1.1 0 EX_OK\r\nContent-length: nope")]
    [DataRow("SPAMD/1.1 1 ERROR\r\nContent-length: 42")]
    public void TryReadContentLength_RejectsInvalidHeaders(string header)
    {
        var ok = SpamAssassinResponseValidator.TryReadContentLength(header, out _);

        Assert.IsFalse(ok);
    }
}
