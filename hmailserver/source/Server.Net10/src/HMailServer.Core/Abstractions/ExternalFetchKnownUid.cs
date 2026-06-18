namespace HMailServer.Core.Abstractions;

public sealed record ExternalFetchKnownUid(
    int Id,
    string Value,
    DateTime CreatedAt);
