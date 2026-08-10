namespace HMailServer.Core.Abstractions;

public interface ISettingsAdministrationMutationStore
{
    ValueTask<bool> UpdateDefaultDomainAsync(
        string defaultDomain,
        CancellationToken cancellationToken);
}
