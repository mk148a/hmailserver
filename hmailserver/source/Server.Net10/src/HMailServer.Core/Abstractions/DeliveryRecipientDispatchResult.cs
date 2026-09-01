namespace HMailServer.Core.Abstractions;

public sealed record DeliveryRecipientDispatchResult(
    long RecipientId,
    DeliveryFailureKind? FailureKind,
    string? Error)
{
    public bool Succeeded => FailureKind is null;
}
