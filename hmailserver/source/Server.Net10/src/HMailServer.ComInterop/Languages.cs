using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("A98C92EF-6AA0-4F22-A29F-BE9154CC242A")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceLanguage
{
    [DispId(1)]
    [SpecialName]
    [return: MarshalAs(UnmanagedType.BStr)]
    string get_String([MarshalAs(UnmanagedType.BStr)] string englishString);

    [DispId(2)]
    string Name
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;
    }

    [DispId(3)]
    bool IsDownloaded
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;
    }

    [DispId(4)]
    void Download();
}

[ComVisible(true)]
[Guid("94720D8A-BC4D-493D-8BDC-8FB28BF31BA5")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceLanguages
{
    [DispId(0)]
    IInterfaceLanguage this[int index] { get; }

    [DispId(1)]
    int Count { get; }

    [DispId(3)]
    [SpecialName]
    IInterfaceLanguage get_ItemByName([MarshalAs(UnmanagedType.BStr)] string itemName);
}

[ComVisible(true)]
[Guid("BE1070A2-9265-495E-B134-27FAA93916CE")]
[ProgId("hMailServer.Languages.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceLanguages))]
public sealed class Languages : IInterfaceLanguages
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly IReadOnlyList<LanguageAdministrationSnapshot>? _languages;

    public Languages()
    {
    }

    private Languages(IReadOnlyList<LanguageAdministrationSnapshot> languages)
    {
        _languages = languages.ToArray();
    }

    public int Count => GetLanguages().Count;

    public IInterfaceLanguage this[int index]
    {
        get
        {
            var languages = GetLanguages();
            if (index < 0 || index >= languages.Count)
            {
                throw new COMException("Language index was outside the collection.", DispEBadIndex);
            }

            return Language.CreateAuthorized(languages[index]);
        }
    }

    public IInterfaceLanguage get_ItemByName(string itemName)
    {
        var match = GetLanguages()
            .FirstOrDefault(language => language.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No language with the specified name exists.", DispEBadIndex)
            : Language.CreateAuthorized(match);
    }

    internal static Languages CreateAuthorized(IReadOnlyList<LanguageAdministrationSnapshot> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        return new Languages(languages);
    }

    private IReadOnlyList<LanguageAdministrationSnapshot> GetLanguages() =>
        _languages
        ?? throw new COMException(
            "Languages access requires an authenticated server administrator.",
            EAccessDenied);
}

[ComVisible(true)]
[Guid("1C70E18B-C63D-458C-B080-64E4F94C4E83")]
[ProgId("hMailServer.Language.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceLanguage))]
public sealed class Language : IInterfaceLanguage
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly LanguageAdministrationSnapshot? _language;

    public Language()
    {
    }

    private Language(LanguageAdministrationSnapshot language)
    {
        _language = language;
    }

    public string Name => Snapshot.Name;

    public bool IsDownloaded => Snapshot.IsDownloaded;

    public string get_String(string englishString) =>
        Snapshot.GetString(englishString ?? string.Empty);

    public void Download()
    {
        _ = Snapshot;
        throw new COMException("Not implemented.", ENotImplemented);
    }

    internal static Language CreateAuthorized(LanguageAdministrationSnapshot language) => new(language);

    private LanguageAdministrationSnapshot Snapshot =>
        _language
        ?? throw new COMException(
            "Language access requires an authenticated server administrator.",
            EAccessDenied);
}

[ComVisible(false)]
public static class LanguageAdministrationRuntimeHost
{
    private const int ENotImplemented = unchecked((int)0x80004001);

    private static ILanguageAdministrationStore? _store;

    public static void Configure(ILanguageAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Languages CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "This GlobalObjects member is not implemented by the .NET 10 rewrite yet.",
                ENotImplemented);

        var languages = store
            .GetLanguagesAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Languages.CreateAuthorized(languages);
    }

    internal static void ResetForTests() => Volatile.Write(ref _store, null);
}
