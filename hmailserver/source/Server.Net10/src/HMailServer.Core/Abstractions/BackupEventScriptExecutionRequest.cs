namespace HMailServer.Core.Abstractions;

public sealed record BackupEventScriptExecutionRequest(
    string EventName,
    string FailureReason = "");
