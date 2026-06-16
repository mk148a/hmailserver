using System.Net.Security;
using System.Net.Sockets;

namespace HMailServer.Protocols.Smtp;

public sealed class StartTlsSmtpConnectionStreamFactory : ISmtpConnectionStreamFactory
{
    private readonly Func<SslServerAuthenticationOptions> _serverAuthenticationOptionsFactory;

    public StartTlsSmtpConnectionStreamFactory(
        Func<SslServerAuthenticationOptions> serverAuthenticationOptionsFactory)
    {
        _serverAuthenticationOptionsFactory = serverAuthenticationOptionsFactory
            ?? throw new ArgumentNullException(nameof(serverAuthenticationOptionsFactory));
    }

    public bool SupportsStartTls => true;

    public ValueTask<Stream> OpenStreamAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Stream>(client.GetStream());
    }

    public async ValueTask<Stream> UpgradeToTlsAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
        await sslStream.AuthenticateAsServerAsync(
            _serverAuthenticationOptionsFactory(),
            cancellationToken).ConfigureAwait(false);
        return sslStream;
    }
}
