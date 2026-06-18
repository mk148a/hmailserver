namespace HMailServer.Core.Abstractions;

public sealed record ExternalAccountDownloadScriptExecutionResult(
    bool Succeeded,
    string? Error,
    byte[]? MessageData,
    ExternalAccountDownloadDeleteAction DeleteAction,
    int DeleteAfterDays)
{
    public static ExternalAccountDownloadScriptExecutionResult Continue(byte[]? messageData = null) =>
        new(
            Succeeded: true,
            Error: null,
            messageData,
            ExternalAccountDownloadDeleteAction.UseAccountDefault,
            DeleteAfterDays: 0);

    public static ExternalAccountDownloadScriptExecutionResult DeleteImmediately(byte[]? messageData = null) =>
        new(
            Succeeded: true,
            Error: null,
            messageData,
            ExternalAccountDownloadDeleteAction.DeleteImmediately,
            DeleteAfterDays: 0);

    public static ExternalAccountDownloadScriptExecutionResult DeleteAfter(
        int days,
        byte[]? messageData = null) =>
        new(
            Succeeded: true,
            Error: null,
            messageData,
            ExternalAccountDownloadDeleteAction.DeleteAfterDays,
            Math.Max(0, days));

    public static ExternalAccountDownloadScriptExecutionResult NeverDelete(byte[]? messageData = null) =>
        new(
            Succeeded: true,
            Error: null,
            messageData,
            ExternalAccountDownloadDeleteAction.NeverDelete,
            DeleteAfterDays: 0);

    public static ExternalAccountDownloadScriptExecutionResult Failure(
        string error,
        byte[]? messageData = null) =>
        new(
            Succeeded: false,
            error,
            messageData,
            ExternalAccountDownloadDeleteAction.UseAccountDefault,
            DeleteAfterDays: 0);
}
