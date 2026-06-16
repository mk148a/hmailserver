using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LegacyPasswordVerifierTests
{
    [TestMethod]
    public void Verify_AcceptsLegacyPlainTextCaseInsensitivePassword()
    {
        Assert.IsTrue(LegacyPasswordVerifier.Verify(
            "Secret",
            "secret",
            LegacyPasswordEncryptionType.None));
    }

    [TestMethod]
    public void Verify_AcceptsLegacyMd5Password()
    {
        Assert.IsTrue(LegacyPasswordVerifier.Verify(
            "secret",
            "5ebe2294ecd0e0f08eab7690d2a6ee69",
            LegacyPasswordEncryptionType.MD5));
    }

    [TestMethod]
    public void Verify_AcceptsLegacySaltedSha256Password()
    {
        Assert.IsTrue(LegacyPasswordVerifier.Verify(
            "secret",
            "abcdefdc0b1e46aec57b30c43e1308ddda569b40829c1a0a541afef902cfae193e8031",
            LegacyPasswordEncryptionType.SHA256));
    }

    [TestMethod]
    public void Verify_AcceptsLegacyBlowFishPassword()
    {
        Assert.IsTrue(LegacyPasswordVerifier.Verify(
            "SECRET",
            "a62b3c438efae3db",
            LegacyPasswordEncryptionType.BlowFish));
    }

    [TestMethod]
    public void BlowfishCipher_UsesLegacyVectors()
    {
        Assert.AreEqual("a62b3c438efae3db", LegacyBlowfishPasswordCipher.Encrypt("secret"));
        Assert.AreEqual("e79ca726380cc3b1", LegacyBlowfishPasswordCipher.Encrypt("Hejsan"));
        Assert.AreEqual("53017df649201454294938b861b56ab2", LegacyBlowfishPasswordCipher.Encrypt("Secret123"));

        Assert.IsTrue(LegacyBlowfishPasswordCipher.TryDecrypt("b075940092833435", out var decrypted));
        Assert.AreEqual("dcidjea", decrypted);
    }

    [TestMethod]
    public void Verify_RejectsEmptyPasswordAndInvalidBlowFish()
    {
        Assert.IsFalse(LegacyPasswordVerifier.Verify(
            string.Empty,
            "secret",
            LegacyPasswordEncryptionType.None));
        Assert.IsFalse(LegacyPasswordVerifier.Verify(
            "secret",
            "encrypted",
            LegacyPasswordEncryptionType.BlowFish));
        Assert.IsFalse(LegacyPasswordVerifier.Verify(
            "secret",
            "0000",
            LegacyPasswordEncryptionType.BlowFish));
    }
}
