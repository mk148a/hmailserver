namespace HMailServer.Core.Abstractions;

public interface ISslCertificateAdministrationStore
{
    ValueTask<IReadOnlyList<SslCertificateAdministrationSnapshot>> GetSslCertificatesAsync(
        CancellationToken cancellationToken);

    ValueTask ClearSslCertificatesAsync(
        CancellationToken cancellationToken);

    ValueTask DeleteSslCertificateByIdAsync(
        int databaseId,
        CancellationToken cancellationToken);
}
