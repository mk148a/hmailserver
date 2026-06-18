using System.Net.Sockets;

namespace HMailServer.Protocols.Pop3;

public interface IPop3ConnectionStreamFactory
{
    ValueTask<Stream> OpenStreamAsync(
        TcpClient client,
        CancellationToken cancellationToken);
}
