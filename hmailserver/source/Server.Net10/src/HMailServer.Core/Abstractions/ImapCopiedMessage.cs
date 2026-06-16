namespace HMailServer.Core.Abstractions;

public sealed record ImapCopiedMessage(
    MessageIdentity SourceIdentity,
    long SourceSequenceNumber,
    MessageIdentity DestinationIdentity,
    long? ExpungeSequenceNumber);
