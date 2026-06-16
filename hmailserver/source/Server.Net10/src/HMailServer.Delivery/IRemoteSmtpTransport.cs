namespace HMailServer.Delivery;

public interface IRemoteSmtpTransport : IAsyncDisposable
{
    Stream Stream { get; }

    ValueTask UpgradeToTlsAsync(
        string targetHost,
        CancellationToken cancellationToken);
}
