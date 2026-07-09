namespace HMailServer.Core.Abstractions;

public interface IServerMessageAdministrationStore
{
    ValueTask<IReadOnlyList<ServerMessageAdministrationSnapshot>> GetServerMessagesAsync(
        CancellationToken cancellationToken);

    ValueTask UpdateServerMessageAsync(
        ServerMessageAdministrationSnapshot message,
        CancellationToken cancellationToken);
}
