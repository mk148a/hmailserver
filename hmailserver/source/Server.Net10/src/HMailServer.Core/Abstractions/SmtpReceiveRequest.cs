namespace HMailServer.Core.Abstractions;

public sealed record SmtpReceiveRequest(
    string HeloHost,
    bool IsExtendedSmtp,
    string MailFrom,
    IReadOnlyList<SmtpResolvedRecipient> Recipients,
    long? DeclaredSize,
    byte[] MessageData,
    DateTimeOffset ReceivedUtc);
