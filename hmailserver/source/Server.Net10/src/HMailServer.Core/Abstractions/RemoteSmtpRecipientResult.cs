namespace HMailServer.Core.Abstractions;

public sealed record RemoteSmtpRecipientResult(
    int RecipientIndex,
    DeliveryFailureKind? FailureKind,
    string? Error)
{
    public bool Accepted => FailureKind is null;
}
