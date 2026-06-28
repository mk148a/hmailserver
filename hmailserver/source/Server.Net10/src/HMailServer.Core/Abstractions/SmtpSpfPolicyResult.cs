namespace HMailServer.Core.Abstractions;

public enum SmtpSpfPolicyStatus
{
    Skipped,
    None,
    Neutral,
    Pass,
    Fail,
    SoftFail,
    TempError,
    PermError
}

public sealed record SmtpSpfPolicyResult(
    bool Evaluated,
    SmtpSpfPolicyStatus Status,
    bool MarkAsSpam,
    bool Passed,
    int Score,
    string Domain,
    string Sender,
    string HeloDomain,
    string? MatchedMechanism,
    string Diagnostic)
{
    public static SmtpSpfPolicyResult Skipped { get; } =
        new(
            Evaluated: false,
            Status: SmtpSpfPolicyStatus.Skipped,
            MarkAsSpam: false,
            Passed: false,
            Score: 0,
            Domain: string.Empty,
            Sender: string.Empty,
            HeloDomain: string.Empty,
            MatchedMechanism: null,
            Diagnostic: string.Empty);

    public static SmtpSpfPolicyResult FromEvaluation(
        SmtpSpfPolicyStatus status,
        int failScore,
        string domain,
        string sender,
        string heloDomain,
        string? matchedMechanism,
        string diagnostic)
    {
        var failed = status == SmtpSpfPolicyStatus.Fail;
        return new SmtpSpfPolicyResult(
            Evaluated: true,
            status,
            MarkAsSpam: failed,
            Passed: status == SmtpSpfPolicyStatus.Pass,
            Score: failed ? Math.Max(0, failScore) : 0,
            domain,
            sender,
            heloDomain,
            matchedMechanism,
            diagnostic);
    }
}
