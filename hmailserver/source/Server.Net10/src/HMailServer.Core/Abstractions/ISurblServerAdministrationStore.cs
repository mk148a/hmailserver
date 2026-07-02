namespace HMailServer.Core.Abstractions;

public interface ISurblServerAdministrationStore
{
    ValueTask<IReadOnlyList<SurblServerAdministrationSnapshot>> GetSurblServersAsync(
        CancellationToken cancellationToken);
}
