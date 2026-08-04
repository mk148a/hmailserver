namespace HMailServer.Core.Abstractions;

public interface ISurblServerAdministrationStore
{
    ValueTask<IReadOnlyList<SurblServerAdministrationSnapshot>> GetSurblServersAsync(
        CancellationToken cancellationToken);

    ValueTask<int> InsertSurblServerAsync(
        SurblServerAdministrationSnapshot server,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("SURBL server insertion is not available in this store.");

    ValueTask<bool> UpdateSurblServerAsync(
        SurblServerAdministrationSnapshot server,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("SURBL server updates are not available in this store.");
}
