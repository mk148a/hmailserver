namespace HMailServer.Core.Abstractions;

public enum SmtpDkimPolicyStatus
{
    Skipped,
    Neutral,
    Pass,
    TempFail,
    PermFail
}

public sealed record SmtpDkimPolicyResult(
    bool Evaluated,
    SmtpDkimPolicyStatus Status,
    bool MarkAsSpam,
    bool Passed,
    int Score,
    string Diagnostic)
{
    public static SmtpDkimPolicyResult Skipped { get; } =
        new(
            Evaluated: false,
            Status: SmtpDkimPolicyStatus.Skipped,
            MarkAsSpam: false,
            Passed: false,
            Score: 0,
            Diagnostic: string.Empty);

    public static SmtpDkimPolicyResult FromEvaluation(
        SmtpDkimPolicyStatus status,
        int failureScore,
        string diagnostic)
    {
        var failed = status == SmtpDkimPolicyStatus.PermFail;
        return new SmtpDkimPolicyResult(
            Evaluated: true,
            status,
            MarkAsSpam: failed,
            Passed: status == SmtpDkimPolicyStatus.Pass,
            Score: failed ? Math.Max(0, failureScore) : 0,
            diagnostic);
    }
}
