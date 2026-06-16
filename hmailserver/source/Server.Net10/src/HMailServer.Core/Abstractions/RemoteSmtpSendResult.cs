namespace HMailServer.Core.Abstractions;

public sealed record RemoteSmtpSendResult(
    bool Succeeded,
    string? Error,
    TimeSpan? RetryDelay,
    DeliveryFailureKind? FailureKind)
{
    public static RemoteSmtpSendResult Success() =>
        new(Succeeded: true, Error: null, RetryDelay: null, FailureKind: null);

    public static RemoteSmtpSendResult Failure(
        string error,
        TimeSpan? retryDelay = null,
        DeliveryFailureKind failureKind = DeliveryFailureKind.Transient) =>
        new(Succeeded: false, Error: error, RetryDelay: retryDelay, FailureKind: failureKind);
}
