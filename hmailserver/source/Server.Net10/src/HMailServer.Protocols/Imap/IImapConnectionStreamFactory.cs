using System.Net.Sockets;

namespace HMailServer.Protocols.Imap;

public interface IImapConnectionStreamFactory
{
    ValueTask<Stream> OpenStreamAsync(
        TcpClient client,
        CancellationToken cancellationToken);
}
