namespace HMailServer.Core.Abstractions;

public sealed record ImapFolderAdministrationSnapshot(
    int Id,
    int AccountId,
    int ParentId,
    string Name,
    bool Subscribed,
    int CurrentUid,
    string CreationTime);
