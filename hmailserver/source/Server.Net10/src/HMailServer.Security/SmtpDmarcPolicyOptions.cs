namespace HMailServer.Security;

public sealed record SmtpDmarcPolicyOptions
{
    public bool Enabled { get; init; }

    public bool SkipAuthenticated { get; init; } = true;

    public bool MarkPolicyFailuresAsSpam { get; init; }

    public int FailureScore { get; init; } = 5;
}
