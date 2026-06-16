namespace HMailServer.Protocols.Imap;

public sealed record ImapSessionContext(
    int? AccountId = null,
    int? FolderId = null,
    string? AccountAddress = null,
    bool IsSecureConnection = false);
