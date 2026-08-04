using HMailServer.Core;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WhiteListMatcherTests
{
    [TestMethod]
    public void IsMatch_UsesInclusiveSameFamilyIpv4Range()
    {
        var entries = new[]
        {
            Entry("192.0.2.10", "192.0.2.20", "sender@example.test")
        };

        Assert.IsFalse(WhiteListMatcher.IsMatch("192.0.2.9", "sender@example.test", entries));
        Assert.IsTrue(WhiteListMatcher.IsMatch("192.0.2.10", "sender@example.test", entries));
        Assert.IsTrue(WhiteListMatcher.IsMatch("192.0.2.20", "sender@example.test", entries));
        Assert.IsFalse(WhiteListMatcher.IsMatch("192.0.2.21", "sender@example.test", entries));
    }

    [TestMethod]
    public void IsMatch_UsesInclusiveIpv6RangeAndRejectsOtherFamily()
    {
        var entries = new[]
        {
            Entry("2001:db8::10", "2001:db8::20", "sender@example.test")
        };

        Assert.IsTrue(WhiteListMatcher.IsMatch("2001:db8::10", "sender@example.test", entries));
        Assert.IsTrue(WhiteListMatcher.IsMatch("2001:db8::20", "sender@example.test", entries));
        Assert.IsFalse(WhiteListMatcher.IsMatch("2001:db8::21", "sender@example.test", entries));
        Assert.IsFalse(WhiteListMatcher.IsMatch("192.0.2.15", "sender@example.test", entries));
    }

    [TestMethod]
    public void IsMatch_EmptyOrStarEmailMatchesAnySender()
    {
        var entries = new[]
        {
            Entry("192.0.2.1", "192.0.2.1", string.Empty),
            Entry("192.0.2.2", "192.0.2.2", "*")
        };

        Assert.IsTrue(WhiteListMatcher.IsMatch("192.0.2.1", "one@example.test", entries));
        Assert.IsTrue(WhiteListMatcher.IsMatch("192.0.2.2", "another@example.test", entries));
    }

    [TestMethod]
    public void IsMatch_WildcardsAreCaseInsensitiveAndQuestionMarkMatchesOneCharacter()
    {
        var entries = new[]
        {
            Entry("192.0.2.1", "192.0.2.1", "Admin+?.Example@*.TEST")
        };

        Assert.IsTrue(WhiteListMatcher.IsMatch("192.0.2.1", "admin+7.example@sender.test", entries));
        Assert.IsTrue(WhiteListMatcher.IsMatch("192.0.2.1", "ADMIN+x.EXAMPLE@SENDER.TEST", entries));
        Assert.IsFalse(WhiteListMatcher.IsMatch("192.0.2.1", "admin+77.example@sender.test", entries));
    }

    [TestMethod]
    public void IsMatch_TreatsNonWildcardCharactersAsLiterals()
    {
        var entries = new[]
        {
            Entry("192.0.2.1", "192.0.2.1", "literal.+(tag)@[example].test")
        };

        Assert.IsTrue(WhiteListMatcher.IsMatch("192.0.2.1", "literal.+(tag)@[example].test", entries));
        Assert.IsFalse(WhiteListMatcher.IsMatch("192.0.2.1", "literalX(tag)@example.test", entries));
    }

    [TestMethod]
    public void IsMatch_InvalidClientOrStoreDataFailsClosed()
    {
        var validSender = new[]
        {
            Entry("192.0.2.1", "192.0.2.1", "sender@example.test")
        };
        var invalidEntries = new[]
        {
            Entry("not-an-ip", "192.0.2.1", "*"),
            Entry("192.0.2.1", "not-an-ip", "*"),
            Entry("192.0.2.20", "192.0.2.10", "*"),
            Entry("192.0.2.1", "2001:db8::1", "*")
        };

        Assert.IsFalse(WhiteListMatcher.IsMatch("not-an-ip", "sender@example.test", validSender));
        Assert.IsFalse(WhiteListMatcher.IsMatch("192.0.2.1", "sender@example.test", invalidEntries));
    }

    private static WhiteListAddressAdministrationSnapshot Entry(
        string lowerIpAddress,
        string upperIpAddress,
        string emailAddress) =>
        new(0, lowerIpAddress, upperIpAddress, emailAddress, string.Empty);
}
