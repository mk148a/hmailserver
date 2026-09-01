namespace HMailServer.Core.Abstractions;

public sealed record DeliveryTargetDispatchResult(
    bool Succeeded,
    string? Error,
    TimeSpan? RetryDelay,
    DeliveryFailureKind? FailureKind,
    IReadOnlyList<DeliveryRecipientDispatchResult>? RecipientResults = null)
{
    public static DeliveryTargetDispatchResult Success(
        IReadOnlyList<DeliveryRecipientDispatchResult>? recipientResults = null) =>
        new(
            Succeeded: true,
            Error: null,
            RetryDelay: null,
            FailureKind: null,
            RecipientResults: recipientResults);

    public static DeliveryTargetDispatchResult TransientFailure(
        string error,
        TimeSpan? retryDelay = null,
        IReadOnlyList<DeliveryRecipientDispatchResult>? recipientResults = null) =>
        new(
            Succeeded: false,
            Error: error,
            RetryDelay: retryDelay,
            FailureKind: DeliveryFailureKind.Transient,
            RecipientResults: recipientResults);

    public static DeliveryTargetDispatchResult PermanentFailure(
        string error,
        IReadOnlyList<DeliveryRecipientDispatchResult>? recipientResults = null) =>
        new(
            Succeeded: false,
            Error: error,
            RetryDelay: null,
            FailureKind: DeliveryFailureKind.Permanent,
            RecipientResults: recipientResults);
}
