namespace HMailServer.Core.Abstractions;

public sealed record ImapAppendRequest(
    int DestinationAccountId,
    int DestinationFolderId,
    string MailboxName,
    byte Flags,
    DateTimeOffset? InternalDateUtc,
    byte[] RawMessage);
