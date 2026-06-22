using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LegacyServerAdministratorAuthenticationProviderTests
{
    [TestMethod]
    public void Authenticate_AcceptsLegacyAdministratorHashesCaseInsensitively()
    {
        var sha256Provider = new LegacyServerAdministratorAuthenticationProvider(
            "abcdefdc0b1e46aec57b30c43e1308ddda569b40829c1a0a541afef902cfae193e8031");
        var md5Provider = new LegacyServerAdministratorAuthenticationProvider(
            "5ebe2294ecd0e0f08eab7690d2a6ee69");

        Assert.IsTrue(sha256Provider.Authenticate("administrator", "secret"));
        Assert.IsTrue(md5Provider.Authenticate("ADMINISTRATOR", "secret"));
    }

    [TestMethod]
    public void Authenticate_RejectsWrongIdentityPasswordAndUnsupportedHash()
    {
        var provider = new LegacyServerAdministratorAuthenticationProvider(
            "abcdefdc0b1e46aec57b30c43e1308ddda569b40829c1a0a541afef902cfae193e8031");
        var unsupportedProvider = new LegacyServerAdministratorAuthenticationProvider("plain-text");

        Assert.IsFalse(provider.Authenticate("user@example.test", "secret"));
        Assert.IsFalse(provider.Authenticate("Administrator", "wrong"));
        Assert.IsFalse(unsupportedProvider.Authenticate("Administrator", "plain-text"));
    }

    [TestMethod]
    public void Authenticate_EmptyStoredHashPreservesLegacyAnonymousAdministratorBoundary()
    {
        var provider = new LegacyServerAdministratorAuthenticationProvider(string.Empty);

        Assert.IsTrue(provider.Authenticate("Administrator", string.Empty));
        Assert.IsFalse(provider.Authenticate("Administrator", "non-empty"));
    }
}
