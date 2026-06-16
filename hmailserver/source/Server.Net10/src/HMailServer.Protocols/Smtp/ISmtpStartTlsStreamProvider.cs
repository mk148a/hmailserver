namespace HMailServer.Protocols.Smtp;

public interface ISmtpStartTlsStreamProvider
{
    bool SupportsStartTls { get; }

    ValueTask<Stream> UpgradeToTlsAsync(
        Stream stream,
        CancellationToken cancellationToken);
}
