namespace HMailServer.Core.Abstractions;

public sealed record DiagnosticResultSnapshot(
    string Name,
    string Description,
    string ExecutionDetails,
    bool Result);
