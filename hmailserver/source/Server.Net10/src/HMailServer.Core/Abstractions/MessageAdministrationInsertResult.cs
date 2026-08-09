namespace HMailServer.Core.Abstractions;

public sealed record MessageAdministrationInsertResult(
    long MessageId,
    long Uid,
    int State);
