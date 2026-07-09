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

    ValueTask UpdateSslCertificateAsync(
        SslCertificateAdministrationSnapshot certificate,
        CancellationToken cancellationToken);

    ValueTask<int> InsertSslCertificateAsync(
        SslCertificateAdministrationSnapshot certificate,
        CancellationToken cancellationToken);
}
