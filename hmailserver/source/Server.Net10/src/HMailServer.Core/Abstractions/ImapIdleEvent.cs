namespace HMailServer.Core.Abstractions;

public sealed record ImapIdleEvent(
    ImapIdleEventKind Kind,
    long Number,
    byte? Flags = null,
    long? Uid = null);
