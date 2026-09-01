namespace HMailServer.Core.Abstractions;

public sealed record RemoteSmtpSendResult(
    bool Succeeded,
    string? Error,
    TimeSpan? RetryDelay,
    DeliveryFailureKind? FailureKind,
    bool TryNextEndpoint = true,
    IReadOnlyList<RemoteSmtpRecipientResult>? RecipientResults = null)
{
    public static RemoteSmtpSendResult Success(
        IReadOnlyList<RemoteSmtpRecipientResult>? recipientResults = null) =>
        new(
            Succeeded: true,
            Error: null,
            RetryDelay: null,
            FailureKind: null,
            TryNextEndpoint: true,
            RecipientResults: recipientResults);

    public static RemoteSmtpSendResult Failure(
        string error,
        TimeSpan? retryDelay = null,
        DeliveryFailureKind failureKind = DeliveryFailureKind.Transient,
        bool tryNextEndpoint = true,
        IReadOnlyList<RemoteSmtpRecipientResult>? recipientResults = null) =>
        new(
            Succeeded: false,
            Error: error,
            RetryDelay: retryDelay,
            FailureKind: failureKind,
            TryNextEndpoint: tryNextEndpoint,
            RecipientResults: recipientResults);
}
