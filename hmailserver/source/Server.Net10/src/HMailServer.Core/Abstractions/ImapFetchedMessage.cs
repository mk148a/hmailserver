namespace HMailServer.Core.Abstractions;

public sealed record ImapFetchedMessage(
    MessageIdentity Identity,
    long SequenceNumber,
    byte Flags,
    long SizeBytes,
    DateTimeOffset InternalDateUtc,
    byte[]? RawMessage);
