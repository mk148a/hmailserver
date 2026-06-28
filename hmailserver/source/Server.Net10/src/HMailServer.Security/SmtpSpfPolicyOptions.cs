namespace HMailServer.Security;

public sealed record SmtpSpfPolicyOptions
{
    public bool Enabled { get; init; }

    public bool SkipAuthenticated { get; init; } = true;

    public int FailScore { get; init; } = 3;
}
