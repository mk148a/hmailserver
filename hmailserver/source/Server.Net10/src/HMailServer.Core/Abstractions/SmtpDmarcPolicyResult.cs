namespace HMailServer.Core.Abstractions;

public enum SmtpDmarcPolicyStatus
{
    Skipped,
    None,
    Pass,
    Fail,
    TempError,
    PermError
}

public enum SmtpDmarcAppliedPolicy
{
    None,
    Quarantine,
    Reject
}

public sealed record SmtpDmarcPolicyResult(
    bool Evaluated,
    SmtpDmarcPolicyStatus Status,
    SmtpDmarcAppliedPolicy AppliedPolicy,
    bool Passed,
    bool MarkAsSpam,
    int Score,
    string HeaderFromDomain,
    string Diagnostic)
{
    public static SmtpDmarcPolicyResult Skipped { get; } =
        new(
            Evaluated: false,
            Status: SmtpDmarcPolicyStatus.Skipped,
            AppliedPolicy: SmtpDmarcAppliedPolicy.None,
            Passed: false,
            MarkAsSpam: false,
            Score: 0,
            HeaderFromDomain: string.Empty,
            Diagnostic: string.Empty);

    public static SmtpDmarcPolicyResult FromEvaluation(
        SmtpDmarcPolicyStatus status,
        SmtpDmarcAppliedPolicy appliedPolicy,
        bool markFailuresAsSpam,
        int failureScore,
        string headerFromDomain,
        string diagnostic)
    {
        var failed = status == SmtpDmarcPolicyStatus.Fail
                     && appliedPolicy != SmtpDmarcAppliedPolicy.None
                     && markFailuresAsSpam;
        return new SmtpDmarcPolicyResult(
            Evaluated: true,
            Status: status,
            AppliedPolicy: appliedPolicy,
            Passed: status == SmtpDmarcPolicyStatus.Pass,
            MarkAsSpam: failed,
            Score: failed ? Math.Max(0, failureScore) : 0,
            HeaderFromDomain: headerFromDomain,
            Diagnostic: diagnostic);
    }
}
