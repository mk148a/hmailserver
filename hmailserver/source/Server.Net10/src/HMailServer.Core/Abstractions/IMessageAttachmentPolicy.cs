namespace HMailServer.Core.Abstractions;

public interface IMessageAttachmentPolicy
{
    ValueTask<MessageAttachmentPolicyResult> ApplyAsync(
        byte[] messageData,
        CancellationToken cancellationToken);
}
