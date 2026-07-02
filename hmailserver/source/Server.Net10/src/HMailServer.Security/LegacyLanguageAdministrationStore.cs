using System.Globalization;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class LegacyLanguageAdministrationStore : ILanguageAdministrationStore
{
    private readonly string _languageDirectory;
    private readonly Func<IReadOnlyList<string>> _validLanguagesLoader;

    public LegacyLanguageAdministrationStore(string applicationBaseDirectory, string initializationFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(initializationFile);

        _languageDirectory = Path.Combine(Path.GetFullPath(applicationBaseDirectory), "Languages");
        var initializationFilePath = Path.GetFullPath(initializationFile);
        _validLanguagesLoader = () => LegacyInitializationFile.LoadValidGuiLanguages(initializationFilePath);
    }

    private LegacyLanguageAdministrationStore(
        string languageDirectory,
        Func<IReadOnlyList<string>> validLanguagesLoader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageDirectory);
        ArgumentNullException.ThrowIfNull(validLanguagesLoader);

        _languageDirectory = Path.GetFullPath(languageDirectory);
        _validLanguagesLoader = validLanguagesLoader;
    }

    public static LegacyLanguageAdministrationStore CreateForDirectory(
        string languageDirectory,
        IEnumerable<string> validLanguages)
    {
        ArgumentNullException.ThrowIfNull(validLanguages);
        var snapshot = validLanguages.ToArray();

        return new LegacyLanguageAdministrationStore(languageDirectory, () => snapshot);
    }

    public ValueTask<IReadOnlyList<LanguageAdministrationSnapshot>> GetLanguagesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_languageDirectory))
        {
            return ValueTask.FromResult<IReadOnlyList<LanguageAdministrationSnapshot>>([]);
        }

        var validLanguages = _validLanguagesLoader();
        if (validLanguages.Count == 0)
        {
            return ValueTask.FromResult<IReadOnlyList<LanguageAdministrationSnapshot>>([]);
        }

        var englishStrings = LoadEnglishStrings();
        var languages = new SortedDictionary<string, LanguageAdministrationSnapshot>(StringComparer.Ordinal);

        foreach (var languageFile in Directory.EnumerateFiles(_languageDirectory, "*.ini", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileNameWithoutExtension(languageFile);
            if (string.IsNullOrEmpty(fileName))
            {
                continue;
            }

            var languageName = fileName.ToLower(CultureInfo.InvariantCulture);
            if (!IsValidLanguage(languageName, validLanguages))
            {
                continue;
            }

            languages[languageName] = new LanguageAdministrationSnapshot(
                languageName,
                IsDownloaded: true,
                LoadTranslatedStrings(languageFile, englishStrings));
        }

        return ValueTask.FromResult<IReadOnlyList<LanguageAdministrationSnapshot>>(languages.Values.ToArray());
    }

    private Dictionary<int, string> LoadEnglishStrings()
    {
        var path = Path.Combine(_languageDirectory, "english.ini");
        var result = new Dictionary<int, string>();

        foreach (var line in SplitLegacyLines(ReadCompleteTextFile(path)))
        {
            if (!line.StartsWith("String_", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryParseLegacyStringLine(line, out var id, out var text))
            {
                result[id] = text;
            }
        }

        return result;
    }

    private static Dictionary<string, string> LoadTranslatedStrings(
        string path,
        IReadOnlyDictionary<int, string> englishStrings)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in SplitLegacyLines(ReadCompleteTextFile(path)))
        {
            if (!TryParseLegacyStringLine(line, out var id, out var text))
            {
                continue;
            }

            if (englishStrings.TryGetValue(id, out var englishString))
            {
                result[englishString] = text;
            }
        }

        return result;
    }

    private static bool TryParseLegacyStringLine(string line, out int id, out string text)
    {
        id = 0;
        text = string.Empty;

        var equalsPosition = line.IndexOf('=', StringComparison.Ordinal);
        if (equalsPosition < "String_".Length)
        {
            return false;
        }

        if (!int.TryParse(
                line["String_".Length..equalsPosition],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out id))
        {
            return false;
        }

        text = line[(equalsPosition + 1)..];
        return true;
    }

    private static string[] SplitLegacyLines(string contents) =>
        contents.Split("\r\n", StringSplitOptions.None);

    private static string ReadCompleteTextFile(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : string.Empty;

    private static bool IsValidLanguage(string languageName, IReadOnlyList<string> validLanguages) =>
        validLanguages.Any(validLanguage => validLanguage.Equals(languageName, StringComparison.OrdinalIgnoreCase));
}
