namespace HMailServer.Core.Abstractions;

public sealed record MessageAdministrationSnapshot(
    long Id,
    int AccountId,
    int FolderId,
    string FileName,
    int State,
    string FromAddress,
    long SizeBytes,
    int CurrentNumberOfTries,
    int Flags,
    DateTime InternalDate,
    long Uid);
