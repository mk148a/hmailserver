namespace HMailServer.Core.Abstractions;

public sealed record RemoteSmtpEndpoint(
    string Host,
    int Port,
    RemoteSmtpConnectionSecurity ConnectionSecurity,
    bool RequiresAuthentication = false,
    string AuthenticationUsername = "",
    string AuthenticationPassword = "",
    string? LocalBindAddress = null,
    IReadOnlyList<string>? HostCandidates = null)
{
    public IReadOnlyList<RemoteSmtpEndpoint> GetCandidates()
    {
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
