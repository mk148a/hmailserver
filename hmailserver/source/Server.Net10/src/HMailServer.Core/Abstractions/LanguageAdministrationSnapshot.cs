namespace HMailServer.Core.Abstractions;

public sealed record LanguageAdministrationSnapshot(
    string Name,
    bool IsDownloaded,
    IReadOnlyDictionary<string, string> Strings)
{
    public string GetString(string englishString)
    {
        ArgumentNullException.ThrowIfNull(englishString);

        return Strings.TryGetValue(englishString, out var translatedString)
            && !string.IsNullOrEmpty(translatedString)
                ? translatedString
                : englishString;
    }
}
