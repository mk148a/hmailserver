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

    private static string CreateTemporaryInitializationFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, contents);
        return path;
    }
}
