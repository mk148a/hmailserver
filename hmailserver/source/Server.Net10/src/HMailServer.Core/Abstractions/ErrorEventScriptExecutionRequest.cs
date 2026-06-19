namespace HMailServer.Core.Abstractions;

public sealed record ErrorEventScriptExecutionRequest(
    int Severity,
    int ErrorCode,
    string Source,
    string Description);
