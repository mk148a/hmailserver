namespace HMailServer.Core.Abstractions;

public sealed record AccountBackupAdministrationSnapshot(
    AccountAdministrationSnapshot Account,
    string Password,
    int PasswordEncryption);
