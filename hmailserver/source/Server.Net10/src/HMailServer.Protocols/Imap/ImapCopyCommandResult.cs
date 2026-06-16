using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed record ImapCopyCommandResult(
    string Response,
    IReadOnlyList<ImapCopiedMessage> Messages,
    int? DestinationAccountId,
    int? DestinationFolderId,
    bool DeleteSource);
