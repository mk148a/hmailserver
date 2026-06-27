namespace HMailServer.Core.Abstractions;

public sealed record GroupMemberAdministrationSnapshot(
    int Id,
    int GroupId,
    int AccountId);
