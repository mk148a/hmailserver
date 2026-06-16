using System.Net.Sockets;

namespace HMailServer.Protocols.Imap;

public sealed class PlainImapConnectionStreamFactory : IImapConnectionStreamFactory
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
