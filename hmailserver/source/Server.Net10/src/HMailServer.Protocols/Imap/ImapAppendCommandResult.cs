using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed record ImapAppendCommandResult(
    string Response,
    ImapAppendResult? AppendResult,
    int? DestinationAccountId,
    int? DestinationFolderId);
