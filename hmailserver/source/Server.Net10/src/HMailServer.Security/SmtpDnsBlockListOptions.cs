namespace HMailServer.Security;

public sealed record SmtpDnsBlockListOptions
{
    public bool Enabled { get; init; }

    public IReadOnlyList<string> Zones { get; init; } = [];

    public bool SkipAuthenticated { get; init; } = true;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    public string RejectionMessageTemplate { get; init; } =
        "554 Rejected by DNS blocklist {ListHost}";
}
