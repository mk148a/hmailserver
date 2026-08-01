namespace HMailServer.Core.Abstractions;

public sealed record FetchAccountBackupAdministrationSnapshot(
    FetchAccountAdministrationSnapshot Account,
    string Password,
    IReadOnlyList<FetchAccountUidBackupAdministrationSnapshot> Uids);

public sealed record FetchAccountUidBackupAdministrationSnapshot(
    string Value,
    string Date);
