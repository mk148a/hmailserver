using System.Net.Security;
using System.Net.Sockets;

namespace HMailServer.Protocols.Pop3;

public sealed class ImplicitTlsPop3ConnectionStreamFactory : IPop3ConnectionStreamFactory
{
    private readonly Func<SslServerAuthenticationOptions> _serverAuthenticationOptionsFactory;

    public ImplicitTlsPop3ConnectionStreamFactory(
        Func<SslServerAuthenticationOptions> serverAuthenticationOptionsFactory)
    {
        _serverAuthenticationOptionsFactory = serverAuthenticationOptionsFactory
            ?? throw new ArgumentNullException(nameof(serverAuthenticationOptionsFactory));
    }

    public async ValueTask<Stream> OpenStreamAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        try
        {
            await sslStream.AuthenticateAsServerAsync(
                _serverAuthenticationOptionsFactory(),
                cancellationToken).ConfigureAwait(false);
            return sslStream;
        }
        catch
        {
            await sslStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
