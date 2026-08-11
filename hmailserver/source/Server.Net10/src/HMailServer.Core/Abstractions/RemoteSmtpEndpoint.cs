namespace HMailServer.Core.Abstractions;

public sealed record RemoteSmtpEndpoint(
    string Host,
    int Port,
    RemoteSmtpConnectionSecurity ConnectionSecurity,
    bool RequiresAuthentication = false,
    string AuthenticationUsername = "",
    string AuthenticationPassword = "",
    string? LocalBindAddress = null,
    IReadOnlyList<string>? HostCandidates = null,
    bool VerifyRemoteSslCertificate = true,
    string? ConnectionAddress = null,
    IReadOnlyList<RemoteSmtpEndpoint>? Candidates = null,
    bool EnforceLocalEndpointGuard = false)
{
    public IReadOnlyList<RemoteSmtpEndpoint> GetCandidates()
    {
        if (Candidates is { Count: > 0 })
        {
            return Candidates
                .Select(candidate => candidate with
                {
                    LocalBindAddress = LocalBindAddress ?? candidate.LocalBindAddress,
                    VerifyRemoteSslCertificate = VerifyRemoteSslCertificate
                })
                .ToArray();
        }

        if (HostCandidates is not { Count: > 0 })
        {
            return [this];
        }

        var candidates = HostCandidates
            .Where(static host => !string.IsNullOrWhiteSpace(host))
            .Select(host => this with
            {
                Host = host.Trim(),
                HostCandidates = null
            })
            .ToArray();

        return candidates.Length == 0
            ? [this with { HostCandidates = null }]
            : candidates;
    }
}
