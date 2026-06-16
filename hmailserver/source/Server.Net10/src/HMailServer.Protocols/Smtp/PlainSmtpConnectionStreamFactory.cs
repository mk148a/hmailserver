using System.Net.Sockets;

namespace HMailServer.Protocols.Smtp;

public sealed class PlainSmtpConnectionStreamFactory : ISmtpConnectionStreamFactory
{
    public bool SupportsStartTls => false;

    public ValueTask<Stream> OpenStreamAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Stream>(client.GetStream());
    }

    public ValueTask<Stream> UpgradeToTlsAsync(
        Stream stream,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("SMTP STARTTLS is not configured.");
}
