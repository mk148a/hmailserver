namespace HMailServer.Core.Abstractions;

public sealed record ExternalFetchRemoteMessage(
    int SequenceNumber,
    string Uid,
    long Size = 0);
