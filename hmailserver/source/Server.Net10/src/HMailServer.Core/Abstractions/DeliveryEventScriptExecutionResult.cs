namespace HMailServer.Core.Abstractions;

public sealed record DeliveryEventScriptExecutionResult(
    bool Succeeded,
    string? Error,
    byte[]? MessageData,
    bool DropMessage)
{
    public static DeliveryEventScriptExecutionResult Continue(byte[]? messageData = null) =>
        new(Succeeded: true, Error: null, messageData, DropMessage: false);

    public static DeliveryEventScriptExecutionResult Drop(byte[]? messageData = null) =>
        new(Succeeded: true, Error: null, messageData, DropMessage: true);

    public static DeliveryEventScriptExecutionResult Failure(string error, byte[]? messageData = null) =>
        new(Succeeded: false, Error: error, messageData, DropMessage: false);
}
