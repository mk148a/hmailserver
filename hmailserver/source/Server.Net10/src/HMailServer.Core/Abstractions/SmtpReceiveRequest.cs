namespace HMailServer.Core.Abstractions;

public sealed record SmtpReceiveRequest(
    string HeloHost,
    bool IsExtendedSmtp,
    string MailFrom,
    IReadOnlyList<SmtpResolvedRecipient> Recipients,
    long? DeclaredSize,
    byte[] MessageData,
    DateTimeOffset ReceivedUtc,
    string ClientIPAddress = "",
    int ClientPort = 0,
    long SessionId = 0,
    string AuthenticatedUsername = "",
    bool IsAuthenticated = false,
    bool IsEncryptedConnection = false,
    bool EnableAntivirusScan = true,
    bool EnableSpamScan = true);
