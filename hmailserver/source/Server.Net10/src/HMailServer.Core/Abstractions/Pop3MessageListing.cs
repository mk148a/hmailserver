namespace HMailServer.Core.Abstractions;

public sealed record Pop3MessageListing(
    long MessageId,
    string Uid,
    long Size);
