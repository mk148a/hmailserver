namespace HMailServer.Core.Abstractions;

public sealed record MessageAttachmentPolicyResult(
    byte[] MessageData,
    bool Modified,
    IReadOnlyList<string> BlockedFileNames)
{
    public static MessageAttachmentPolicyResult Unchanged(byte[] messageData) =>
        new(messageData, Modified: false, BlockedFileNames: []);
}
