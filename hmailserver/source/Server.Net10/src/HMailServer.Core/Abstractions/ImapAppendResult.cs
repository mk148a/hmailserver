namespace HMailServer.Core.Abstractions;

public sealed record ImapAppendResult(
    MessageIdentity Identity,
    long UidValidity);
