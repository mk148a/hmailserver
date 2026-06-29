namespace HMailServer.Security;

public enum DkimResult
{
    Neutral = 0,
    Pass = 1,
    TempFail = 2,
    PermFail = 3
}

public enum DkimCanonicalizationMethod
{
    Simple = 1,
    Relaxed = 2
}

public sealed record DkimEvaluation(
    DkimResult Result,
    string Diagnostic,
    IReadOnlyList<string> PassingDomains)
{
    public DkimEvaluation(DkimResult result, string diagnostic)
        : this(result, diagnostic, Array.Empty<string>())
    {
    }
}

public sealed record DkimSignature(
    string Version,
    string Algorithm,
    string Domain,
    string Selector,
    IReadOnlyList<string> SignedHeaders,
    string BodyHash,
    string Signature,
    DkimCanonicalizationMethod HeaderCanonicalization,
    DkimCanonicalizationMethod BodyCanonicalization,
    string QueryMethod,
    string? Identity,
    int? BodyLength,
    IReadOnlyDictionary<string, string> Tags);
