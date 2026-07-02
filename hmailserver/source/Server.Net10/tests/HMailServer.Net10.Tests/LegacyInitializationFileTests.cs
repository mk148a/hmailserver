using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LegacyInitializationFileTests
{
    [TestMethod]
    public void ResolvePath_UsesConfiguredPathOrLegacyExecutableDirectoryDefault()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.AreEqual(
            Path.Combine(baseDirectory, "hMailServer.ini"),
            LegacyInitializationFile.ResolvePath(null, baseDirectory));
        Assert.AreEqual(
            Path.Combine(baseDirectory, "config", "server.ini"),
            LegacyInitializationFile.ResolvePath(Path.Combine("config", "server.ini"), baseDirectory));
    }

    [TestMethod]
    public void LoadAdministratorPasswordHash_ReadsAndTrimsLegacySecurityValue()
    {
        var path = CreateTemporaryInitializationFile(
            "[Database]\nType=MSSQL\n\n[Security]\n"
            + "AdministratorPassword=  0123456789abcdef0123456789abcdef  \n");

        try
        {
            Assert.AreEqual(
                "0123456789abcdef0123456789abcdef",
                LegacyInitializationFile.LoadAdministratorPasswordHash(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void LoadAdministratorPasswordHash_PreservesLegacyEmptyHashForMissingFileOrKey()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        var pathWithoutSecurityValue = CreateTemporaryInitializationFile("[Database]\nType=MSSQL");

        try
        {
            Assert.AreEqual(string.Empty, LegacyInitializationFile.LoadAdministratorPasswordHash(missingPath));
            Assert.AreEqual(string.Empty, LegacyInitializationFile.LoadAdministratorPasswordHash(pathWithoutSecurityValue));
        }
        finally
        {
            File.Delete(pathWithoutSecurityValue);
        }
    }

    [TestMethod]
    public void LoadUserInterfaceLanguage_ReadsLegacySettingAndPreservesEnglishDefault()
    {
        var configuredPath = CreateTemporaryInitializationFile("[Settings]\nUseLanguage=Swedish\n");
        var missingKeyPath = CreateTemporaryInitializationFile("[Settings]\nRewriteEnvelopeFromWhenForwarding=1\n");
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");

        try
        {
            Assert.AreEqual("Swedish", LegacyInitializationFile.LoadUserInterfaceLanguage(configuredPath));
            Assert.AreEqual("English", LegacyInitializationFile.LoadUserInterfaceLanguage(missingKeyPath));
            Assert.AreEqual("English", LegacyInitializationFile.LoadUserInterfaceLanguage(missingPath));
        }
        finally
        {
            File.Delete(configuredPath);
            File.Delete(missingKeyPath);
        }
    }

    [TestMethod]
    public void LoadValidGuiLanguages_ReadsLegacyGuiLanguageList()
    {
        var configuredPath = CreateTemporaryInitializationFile(
            "[GUILanguages]\nValidLanguages=english,swedish,turkish\n");
        var missingKeyPath = CreateTemporaryInitializationFile("[Settings]\nUseLanguage=English\n");
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");

        try
        {
            CollectionAssert.AreEqual(
                new[] { "english", "swedish", "turkish" },
                LegacyInitializationFile.LoadValidGuiLanguages(configuredPath).ToArray());
            Assert.AreEqual(0, LegacyInitializationFile.LoadValidGuiLanguages(missingKeyPath).Count);
            Assert.AreEqual(0, LegacyInitializationFile.LoadValidGuiLanguages(missingPath).Count);
        }
        finally
        {
            File.Delete(configuredPath);
            File.Delete(missingKeyPath);
        }
    }

    [TestMethod]
    public void LoadRewriteEnvelopeFromWhenForwarding_EnablesOnlyLegacyIntegerOne()
    {
        var enabledPath = CreateTemporaryInitializationFile(
            "[Settings]\nRewriteEnvelopeFromWhenForwarding=1\n");
        var disabledPath = CreateTemporaryInitializationFile(
            "[Settings]\nRewriteEnvelopeFromWhenForwarding=2\n");
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");

        try
        {
            Assert.IsTrue(LegacyInitializationFile.LoadRewriteEnvelopeFromWhenForwarding(enabledPath));
            Assert.IsFalse(LegacyInitializationFile.LoadRewriteEnvelopeFromWhenForwarding(disabledPath));
            Assert.IsFalse(LegacyInitializationFile.LoadRewriteEnvelopeFromWhenForwarding(missingPath));
        }
        finally
        {
            File.Delete(enabledPath);
            File.Delete(disabledPath);
        }
    }

    private static string CreateTemporaryInitializationFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, contents);
        return path;
    }
}
