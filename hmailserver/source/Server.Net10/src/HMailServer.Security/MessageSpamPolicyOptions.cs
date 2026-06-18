namespace HMailServer.Security;

public sealed record MessageSpamPolicyOptions
{
    public bool AddSpamHeader { get; init; }

    public bool AddReasonHeaders { get; init; }

    public bool PrependSubject { get; init; }

    public int SpamMarkThreshold { get; init; }

    public string SubjectPrefix { get; init; } = "[SPAM]";

    public int MaxHeaderValueLength { get; init; } = 900;
}
