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
    IReadOnlyList<string> PassingDomains,
    string Diagnostic)
{
    public static SmtpDkimPolicyResult Skipped { get; } =
        new(
            Evaluated: false,
            Status: SmtpDkimPolicyStatus.Skipped,
            MarkAsSpam: false,
            Passed: false,
            Score: 0,
            PassingDomains: Array.Empty<string>(),
            Diagnostic: string.Empty);

    public static SmtpDkimPolicyResult FromEvaluation(
        SmtpDkimPolicyStatus status,
        int failureScore,
        string diagnostic,
        IReadOnlyList<string>? passingDomains = null)
    {
        var failed = status == SmtpDkimPolicyStatus.PermFail;
        return new SmtpDkimPolicyResult(
            Evaluated: true,
            status,
            MarkAsSpam: failed,
            Passed: status == SmtpDkimPolicyStatus.Pass,
            Score: failed ? Math.Max(0, failureScore) : 0,
            PassingDomains: status == SmtpDkimPolicyStatus.Pass
                ? passingDomains ?? Array.Empty<string>()
                : Array.Empty<string>(),
            diagnostic);
    }
}
