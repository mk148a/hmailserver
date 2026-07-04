namespace HMailServer.Core.Abstractions;

public interface ILogonFailureAdministrationStore
{
    ValueTask ClearLegacyListAsync(CancellationToken cancellationToken);
}
