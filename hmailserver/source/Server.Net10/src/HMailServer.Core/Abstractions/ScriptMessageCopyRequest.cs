namespace HMailServer.Core.Abstractions;

public sealed record ScriptMessageCopyRequest(
    int SourceAccountId,
    int DestinationFolderId,
    string FromAddress,
    byte Flags,
    DateTimeOffset CreatedUtc,
    byte[] MessageData);
