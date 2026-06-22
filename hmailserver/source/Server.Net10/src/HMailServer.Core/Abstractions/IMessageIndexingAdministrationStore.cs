namespace HMailServer.Core.Abstractions;

public interface IMessageIndexingAdministrationStore
{
    ValueTask<MessageIndexingAdministrationStatus> GetStatusAsync(
        CancellationToken cancellationToken);

    ValueTask<bool> IsEnabledAsync(CancellationToken cancellationToken);

    ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken);

    ValueTask ClearAsync(CancellationToken cancellationToken);

    ValueTask IndexAsync(CancellationToken cancellationToken);

    ValueTask RebuildAsync(CancellationToken cancellationToken);
}
