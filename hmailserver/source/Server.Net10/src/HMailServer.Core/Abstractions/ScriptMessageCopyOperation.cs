namespace HMailServer.Core.Abstractions;

public sealed record ScriptMessageCopyOperation(
    int DestinationFolderId,
    byte[] MessageData);
