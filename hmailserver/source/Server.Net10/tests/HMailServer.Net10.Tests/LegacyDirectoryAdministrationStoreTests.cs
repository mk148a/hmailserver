using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LegacyDirectoryAdministrationStoreTests
{
    [TestMethod]
    public async Task GetDirectoriesAsync_ReadsLegacyIniDirectoryValuesWithLegacyNormalization()
    {
        var path = CreateTemporaryInitializationFile(
            "[Directories]\n"
            + @"ProgramFolder=C:\hMailServer" + "\n"
            + @"DataFolder=C:\hMailServer\Data\" + "\n"
            + @"TempFolder=C:\hMailServer\Temp\" + "\n"
            + @"EventFolder=C:\hMailServer\Events\" + "\n"
            + @"DatabaseFolder=C:\hMailServer\Database\" + "\n"
            + @"LogFolder=C:\hMailServer\Logs\" + "\n");

        try
        {
            var store = new LegacyDirectoryAdministrationStore(path);

            var snapshot = await store.GetDirectoriesAsync(CancellationToken.None);

            Assert.AreEqual(@"C:\hMailServer\", snapshot.ProgramDirectory);
            Assert.AreEqual(@"C:\hMailServer\Database", snapshot.DatabaseDirectory);
            Assert.AreEqual(@"C:\hMailServer\Data", snapshot.DataDirectory);
            Assert.AreEqual(@"C:\hMailServer\Logs\", snapshot.LogDirectory);
            Assert.AreEqual(@"C:\hMailServer\Temp", snapshot.TempDirectory);
            Assert.AreEqual(@"C:\hMailServer\Events\", snapshot.EventDirectory);
            Assert.AreEqual(@"C:\hMailServer\DBScripts", snapshot.DBScriptDirectory);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task GetDirectoriesAsync_PreservesLegacyEmptyDefaultsForMissingIni()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        var store = new LegacyDirectoryAdministrationStore(missingPath);

        var snapshot = await store.GetDirectoriesAsync(CancellationToken.None);

        Assert.AreEqual(@"\", snapshot.ProgramDirectory);
        Assert.AreEqual(string.Empty, snapshot.DatabaseDirectory);
        Assert.AreEqual(string.Empty, snapshot.DataDirectory);
        Assert.AreEqual(string.Empty, snapshot.LogDirectory);
        Assert.AreEqual(string.Empty, snapshot.TempDirectory);
        Assert.AreEqual(string.Empty, snapshot.EventDirectory);
        Assert.AreEqual(@"\DBScripts", snapshot.DBScriptDirectory);
    }

    private static string CreateTemporaryInitializationFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, contents);
        return path;
    }
}
