using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace HMailServer.Security;

public static class TlsServerAuthenticationOptionsFactory
{
    public static SslServerAuthenticationOptions Create(
        X509Certificate2 serverCertificate,
        bool requireClientCertificate = false,
        IReadOnlyList<SslApplicationProtocol>? applicationProtocols = null)
    {
        ArgumentNullException.ThrowIfNull(serverCertificate);

        return new SslServerAuthenticationOptions
        {
            ServerCertificate = serverCertificate,
            ClientCertificateRequired = requireClientCertificate,
            CertificateRevocationCheckMode = X509RevocationMode.Online,
            EnabledSslProtocols = SslProtocols.None,
            ApplicationProtocols = applicationProtocols?.ToList()
        };
    }
}
