namespace HMailServer.Core.Abstractions;

public sealed record TcpIpPortAdministrationSnapshot(
    int Id,
    int Protocol,
    int PortNumber,
    string Address,
    int ConnectionSecurity,
    int SslCertificateId)
{
    public string? SslCertificateName { get; init; }
}
