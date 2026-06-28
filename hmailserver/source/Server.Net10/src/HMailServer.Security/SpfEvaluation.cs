using System.Net;

namespace HMailServer.Security;

public enum SpfResult
{
    None,
    Neutral,
    Pass,
    Fail,
    SoftFail,
    TempError,
    PermError
}

public sealed record SpfEvaluationRequest(
    IPAddress ClientAddress,
    string Domain,
    string Sender,
    string HeloDomain);

public sealed record SpfEvaluation(
    SpfResult Result,
    string Domain,
    string? MatchedMechanism,
    int DnsTermCount,
    int VoidLookupCount,
    string Diagnostic);

public sealed record SpfEvaluatorOptions
{
    public int MaxDnsTerms { get; init; } = 10;

    public int MaxVoidLookups { get; init; } = 2;

    public int MaxMxHosts { get; init; } = 10;

    public int MaxPtrHosts { get; init; } = 10;

    public int MaxRecursionDepth { get; init; } = 10;

    public TimeSpan EvaluationTimeout { get; init; } = TimeSpan.FromSeconds(20);
}
