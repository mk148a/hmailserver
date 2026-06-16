using System.Net.Sockets;

namespace HMailServer.Protocols.Smtp;

public interface ISmtpConnectionStreamFactory : ISmtpStartTlsStreamProvider
{
    ValueTask<Stream> OpenStreamAsync(
        TcpClient client,
        CancellationToken cancellationToken);
}
