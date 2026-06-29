namespace HMailServer.Security;

public sealed record SmtpDkimPolicyOptions
{
    public bool Enabled { get; init; }

    public bool SkipAuthenticated { get; init; } = true;

    public int FailureScore { get; init; } = 5;
}
