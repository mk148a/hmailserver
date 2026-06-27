namespace HMailServer.Core.Abstractions;

public interface ISslCertificateAdministrationStore
{
    ValueTask<IReadOnlyList<SslCertificateAdministrationSnapshot>> GetSslCertificatesAsync(
        CancellationToken cancellationToken);
}
