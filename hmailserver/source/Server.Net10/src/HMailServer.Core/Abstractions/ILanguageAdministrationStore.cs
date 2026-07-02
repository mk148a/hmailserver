namespace HMailServer.Core.Abstractions;

public interface ILanguageAdministrationStore
{
    ValueTask<IReadOnlyList<LanguageAdministrationSnapshot>> GetLanguagesAsync(CancellationToken cancellationToken);
}
