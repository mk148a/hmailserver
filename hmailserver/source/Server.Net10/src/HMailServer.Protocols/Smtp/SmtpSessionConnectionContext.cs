namespace HMailServer.Protocols.Smtp;

public sealed record SmtpSessionConnectionContext(
    string ClientIPAddress = "",
    int ClientPort = 0,
    long SessionId = 0);
