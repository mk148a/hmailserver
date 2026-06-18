namespace HMailServer.Security;

public sealed record SmtpUrlBlockListOptions
{
    public bool Enabled { get; init; }

    public IReadOnlyList<string> Zones { get; init; } = [];

    public bool SkipAuthenticated { get; init; } = true;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    public int MaxHosts { get; init; } = 50;

    public int MaxCandidateDomainsPerHost { get; init; } = 3;

    public string RejectionMessageTemplate { get; init; } =
        "554 Rejected by URL blocklist {ListHost}";
}
