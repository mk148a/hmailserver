namespace HMailServer.Protocols.Imap;

public sealed record ImapAppendCommand(
    string MailboxName,
    byte Flags,
    DateTimeOffset? InternalDateUtc,
    int LiteralByteCount);
