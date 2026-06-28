using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LegacyDatabaseConfigurationTests
{
    [TestMethod]
    public void LoadDatabaseConfiguration_ReadsLegacyDatabaseSettings()
    {
        var path = CreateTemporaryInitializationFile(
            "[Database]\n"
            + "Type=MSSQL\n"
            + @"Server=.\SQLExpress" + "\n"
            + "Database=hmailserver\n");

        try
        {
            var configuration = LegacyInitializationFile.LoadDatabaseConfiguration(path);

            Assert.AreEqual(2, configuration.DatabaseType);
            Assert.IsTrue(configuration.DatabaseExists);
            Assert.AreEqual(@".\SQLExpress", configuration.ServerName);
            Assert.AreEqual("hmailserver", configuration.DatabaseName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void LoadDatabaseConfiguration_PreservesLegacyUnknownTypeForMissingIni()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");

        var configuration = LegacyInitializationFile.LoadDatabaseConfiguration(missingPath);

        Assert.AreEqual(0, configuration.DatabaseType);
        Assert.IsFalse(configuration.DatabaseExists);
        Assert.AreEqual(string.Empty, configuration.ServerName);
        Assert.AreEqual(string.Empty, configuration.DatabaseName);
    }

    private static string CreateTemporaryInitializationFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, contents);
        return path;
    }
}
