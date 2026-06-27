namespace HMailServer.Core.Abstractions;

public sealed record SslCertificateAdministrationSnapshot(
    int Id,
    string Name,
    string CertificateFile,
    string PrivateKeyFile);
