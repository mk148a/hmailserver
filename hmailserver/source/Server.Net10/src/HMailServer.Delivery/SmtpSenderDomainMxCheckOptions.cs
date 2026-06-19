namespace HMailServer.Delivery;

public sealed record SmtpSenderDomainMxCheckOptions
{
    public bool Enabled { get; init; }

    public bool SkipAuthenticated { get; init; } = true;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    public string RejectionMessageTemplate { get; init; } =
        "554 Sender domain does not have any MX records";
}
