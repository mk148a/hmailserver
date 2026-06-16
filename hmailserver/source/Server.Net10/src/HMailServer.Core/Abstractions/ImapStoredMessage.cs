namespace HMailServer.Core.Abstractions;

public sealed record ImapStoredMessage(
    MessageIdentity Identity,
    long SequenceNumber,
    byte Flags);
