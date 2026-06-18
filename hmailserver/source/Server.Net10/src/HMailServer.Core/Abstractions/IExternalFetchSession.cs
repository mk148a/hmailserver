namespace HMailServer.Core.Abstractions;

public interface IExternalFetchSession : IAsyncDisposable
{
    ValueTask<IReadOnlyList<ExternalFetchRemoteMessage>> ListMessagesAsync(
        CancellationToken cancellationToken);

    ValueTask<byte[]> DownloadMessageAsync(
        ExternalFetchRemoteMessage message,
        CancellationToken cancellationToken);

    ValueTask DeleteMessageAsync(
        ExternalFetchRemoteMessage message,
        CancellationToken cancellationToken);
}
