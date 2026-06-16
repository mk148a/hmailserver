namespace HMailServer.Core.Abstractions;

public sealed record ImapExpungedMessage(
    MessageIdentity Identity,
    long SequenceNumber);
