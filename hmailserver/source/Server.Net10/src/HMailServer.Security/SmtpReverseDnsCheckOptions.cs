namespace HMailServer.Security;

public sealed record SmtpReverseDnsCheckOptions
{
    public bool Enabled { get; init; }

    public bool SkipAuthenticated { get; init; } = true;

    public bool RequireForwardConfirmed { get; init; } = true;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    public string RejectionMessageTemplate { get; init; } =
        "554 Rejected by reverse DNS check {Reason}";
}
