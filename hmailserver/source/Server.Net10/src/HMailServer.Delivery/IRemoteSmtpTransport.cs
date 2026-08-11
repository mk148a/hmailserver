namespace HMailServer.Delivery;

public interface IRemoteSmtpTransport : IAsyncDisposable
{
    Stream Stream { get; }

    ValueTask UpgradeToTlsAsync(
        string targetHost,
        bool verifyRemoteSslCertificate,
        CancellationToken cancellationToken);
}
