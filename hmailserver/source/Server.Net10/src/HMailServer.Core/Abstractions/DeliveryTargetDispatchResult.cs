namespace HMailServer.Core.Abstractions;

public sealed record DeliveryTargetDispatchResult(
    bool Succeeded,
    string? Error,
    TimeSpan? RetryDelay,
    DeliveryFailureKind? FailureKind)
{
    public static DeliveryTargetDispatchResult Success() =>
        new(Succeeded: true, Error: null, RetryDelay: null, FailureKind: null);

    public static DeliveryTargetDispatchResult TransientFailure(
        string error,
        TimeSpan? retryDelay = null) =>
        new(Succeeded: false, Error: error, RetryDelay: retryDelay, FailureKind: DeliveryFailureKind.Transient);

    public static DeliveryTargetDispatchResult PermanentFailure(string error) =>
        new(Succeeded: false, Error: error, RetryDelay: null, FailureKind: DeliveryFailureKind.Permanent);
}
