namespace HMailServer.Core.Abstractions;

public sealed record RemoteSmtpSendResult(
    bool Succeeded,
    string? Error,
    TimeSpan? RetryDelay,
    DeliveryFailureKind? FailureKind,
    bool TryNextEndpoint = true)
{
    public static RemoteSmtpSendResult Success() =>
        new(Succeeded: true, Error: null, RetryDelay: null, FailureKind: null, TryNextEndpoint: true);

    public static RemoteSmtpSendResult Failure(
        string error,
        TimeSpan? retryDelay = null,
        DeliveryFailureKind failureKind = DeliveryFailureKind.Transient,
        bool tryNextEndpoint = true) =>
        new(
            Succeeded: false,
            Error: error,
            RetryDelay: retryDelay,
            FailureKind: failureKind,
            TryNextEndpoint: tryNextEndpoint);
}
