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
    public void SaveAdministratorPasswordHash_AtomicallyReplacesHashAndPreservesOtherSettings()
    {
        var path = CreateTemporaryInitializationFile(
            "[Settings]\nUseLanguage=English\n\n[Security]\n"
            + "AdministratorPassword=old-hash\nOtherValue=preserved\n");

        try
        {
            Assert.IsTrue(LegacyInitializationFile.SaveAdministratorPasswordHash(path, "new-hash"));
            Assert.AreEqual("new-hash", LegacyInitializationFile.LoadAdministratorPasswordHash(path));
            StringAssert.Contains(File.ReadAllText(path), "UseLanguage=English");
            StringAssert.Contains(File.ReadAllText(path), "OtherValue=preserved");
            Assert.IsFalse(Directory.GetFiles(Path.GetDirectoryName(path)!, $"{Path.GetFileName(path)}.*.ini").Any());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void SaveAdministratorPasswordHash_CreatesMissingFileAndCleansTemporarySibling()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");

        try
        {
            Assert.IsTrue(LegacyInitializationFile.SaveAdministratorPasswordHash(path, "new-hash"));
            Assert.AreEqual("new-hash", LegacyInitializationFile.LoadAdministratorPasswordHash(path));
            Assert.IsFalse(Directory.GetFiles(Path.GetDirectoryName(path)!, $"{Path.GetFileName(path)}.*.ini").Any());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void SaveAdministratorPasswordHash_FlushesContainingDirectoryAfterReplacement()
    {
        var path = CreateTemporaryInitializationFile(
            "[Security]\nAdministratorPassword=old-hash\n");
        var flushedDirectories = new List<string>();

        try
        {
            Assert.IsTrue(
                LegacyInitializationFile.SaveAdministratorPasswordHash(
                    path,
                    "new-hash",
                    flushedDirectories.Add));
            CollectionAssert.AreEqual(
                new[] { Path.GetDirectoryName(path)! },
                flushedDirectories);
            Assert.AreEqual("new-hash", LegacyInitializationFile.LoadAdministratorPasswordHash(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void SaveUserInterfaceLanguage_WritesLegacySettingAndPreservesOtherSettings()
    {
        var path = CreateTemporaryInitializationFile(
            "[Settings]\nUseLanguage=English\nRewriteEnvelopeFromWhenForwarding=1\n");

        try
        {
            LegacyInitializationFile.SaveUserInterfaceLanguage(path, "Turkish");

            Assert.AreEqual("Turkish", LegacyInitializationFile.LoadUserInterfaceLanguage(path));
            Assert.IsTrue(LegacyInitializationFile.LoadRewriteEnvelopeFromWhenForwarding(path));
        }
        finally
        {
            File.Delete(path);
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

    [TestMethod]
    public void SaveRewriteEnvelopeFromWhenForwarding_WritesLegacyIntegerValues()
    {
        var path = CreateTemporaryInitializationFile("[Settings]\nRewriteEnvelopeFromWhenForwarding=1\n");

        try
        {
            LegacyInitializationFile.SaveRewriteEnvelopeFromWhenForwarding(path, false);
            Assert.IsFalse(LegacyInitializationFile.LoadRewriteEnvelopeFromWhenForwarding(path));

            LegacyInitializationFile.SaveRewriteEnvelopeFromWhenForwarding(path, true);
            Assert.IsTrue(LegacyInitializationFile.LoadRewriteEnvelopeFromWhenForwarding(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void LoadBackupMessagesDbOnly_EnablesOnlyLegacyIntegerOne()
    {
        var enabledPath = CreateTemporaryInitializationFile(
            "[Settings]\nBackupMessagesDBOnly=1\n");
        var disabledPath = CreateTemporaryInitializationFile(
            "[Settings]\nBackupMessagesDBOnly=0\n");
        var missingKeyPath = CreateTemporaryInitializationFile("[Settings]\n");
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");

        try
        {
            Assert.IsTrue(LegacyInitializationFile.LoadBackupMessagesDbOnly(enabledPath));
            Assert.IsFalse(LegacyInitializationFile.LoadBackupMessagesDbOnly(disabledPath));
            Assert.IsFalse(LegacyInitializationFile.LoadBackupMessagesDbOnly(missingKeyPath));
            Assert.IsFalse(LegacyInitializationFile.LoadBackupMessagesDbOnly(missingPath));
        }
        finally
        {
            File.Delete(enabledPath);
            File.Delete(disabledPath);
            File.Delete(missingKeyPath);
        }
    }

    private static string CreateTemporaryInitializationFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, contents);
        return path;
    }
}
