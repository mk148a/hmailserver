using System.Net.Sockets;

namespace HMailServer.Protocols.Pop3;

public sealed class PlainPop3ConnectionStreamFactory : IPop3ConnectionStreamFactory
{
    public ValueTask<Stream> OpenStreamAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Stream>(client.GetStream());
    }
}
