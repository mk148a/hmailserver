using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LegacyLanguageAdministrationStoreTests
{
    [TestMethod]
    public async Task GetLanguagesAsync_LoadsOnlyValidLocalIniFilesInLegacyLowercaseOrder()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            WriteLanguageFile(
                directory,
                "english.ini",
                "Ignored header\r\nString_1=Hello\r\nString_2=Empty fallback\r\nString_3=Only English\r\n");
            WriteLanguageFile(
                directory,
                "Swedish.ini",
                "String_1=Hej\r\nString_2=\r\nString_999=Ignored because no English source\r\n");
            WriteLanguageFile(
                directory,
                "turkish.ini",
                "String_1=Merhaba\r\nString_3=Sadece Ingilizce degil\r\n");
            WriteLanguageFile(directory, "ignored.ini", "String_1=Ignored\r\n");
            File.WriteAllText(Path.Combine(directory, "not-a-language.txt"), "String_1=Ignored\r\n");

            var store = LegacyLanguageAdministrationStore.CreateForDirectory(
                directory,
                new[] { "turkish", "swedish" });

            var languages = await store.GetLanguagesAsync(CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { "swedish", "turkish" },
                languages.Select(static language => language.Name).ToArray());
            Assert.IsTrue(languages.All(static language => language.IsDownloaded));
            Assert.AreEqual("Hej", languages[0].GetString("Hello"));
            Assert.AreEqual("Empty fallback", languages[0].GetString("Empty fallback"));
            Assert.AreEqual("Only English", languages[0].GetString("Only English"));
            Assert.AreEqual("Merhaba", languages[1].GetString("Hello"));
            Assert.AreEqual("Sadece Ingilizce degil", languages[1].GetString("Only English"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetLanguagesAsync_UsesApplicationBaseLanguagesDirectoryAndIniValidLanguageList()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var languageDirectory = Path.Combine(root, "Languages");
            Directory.CreateDirectory(languageDirectory);
            WriteLanguageFile(languageDirectory, "english.ini", "String_1=Hello\r\n");
            WriteLanguageFile(languageDirectory, "swedish.ini", "String_1=Hej\r\n");
            var initializationFile = Path.Combine(root, "hMailServer.ini");
            File.WriteAllText(initializationFile, "[GUILanguages]\r\nValidLanguages=english,swedish\r\n");

            var store = new LegacyLanguageAdministrationStore(root, initializationFile);

            var languages = await store.GetLanguagesAsync(CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { "english", "swedish" },
                languages.Select(static language => language.Name).ToArray());
            Assert.AreEqual("Hej", languages[1].GetString("Hello"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetLanguagesAsync_PreservesEmptyCollectionForMissingDirectoryOrNoValidLanguages()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            WriteLanguageFile(directory, "english.ini", "String_1=Hello\r\n");

            var noValidLanguages = await LegacyLanguageAdministrationStore
                .CreateForDirectory(directory, Array.Empty<string>())
                .GetLanguagesAsync(CancellationToken.None);
            var missingDirectory = await LegacyLanguageAdministrationStore
                .CreateForDirectory(Path.Combine(directory, "missing"), new[] { "english" })
                .GetLanguagesAsync(CancellationToken.None);

            Assert.AreEqual(0, noValidLanguages.Count);
            Assert.AreEqual(0, missingDirectory.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteLanguageFile(string directory, string fileName, string contents) =>
        File.WriteAllText(Path.Combine(directory, fileName), contents);
}
