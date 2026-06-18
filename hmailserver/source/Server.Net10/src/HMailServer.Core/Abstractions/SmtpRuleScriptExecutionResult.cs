namespace HMailServer.Core.Abstractions;

public sealed record SmtpRuleScriptExecutionResult(
    bool Accepted,
    string? FailureResponse,
    byte[]? MessageData,
    bool DropMessage,
    int ResultValue = 0,
    int ResultParameter = 0)
{
    public static SmtpRuleScriptExecutionResult Continue(byte[]? messageData = null) =>
        new(Accepted: true, FailureResponse: null, messageData, DropMessage: false);

    public static SmtpRuleScriptExecutionResult Drop(byte[]? messageData = null) =>
        new(Accepted: true, FailureResponse: null, messageData, DropMessage: true);

    public static SmtpRuleScriptExecutionResult Failure(string response, byte[]? messageData = null) =>
        new(Accepted: false, FailureResponse: response, messageData, DropMessage: false);

    public SmtpRuleScriptExecutionResult WithResult(
        int resultValue,
        int resultParameter) =>
        this with
        {
            ResultValue = resultValue,
            ResultParameter = resultParameter
        };
}
