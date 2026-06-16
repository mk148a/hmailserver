namespace HMailServer.Core.Abstractions;

public sealed record DeliveryBounceResult(
    bool Submitted,
    string? Reason)
{
    public static DeliveryBounceResult SubmittedResult() =>
        new(Submitted: true, Reason: null);

    public static DeliveryBounceResult Skipped(string reason) =>
        new(Submitted: false, Reason: reason);
}
