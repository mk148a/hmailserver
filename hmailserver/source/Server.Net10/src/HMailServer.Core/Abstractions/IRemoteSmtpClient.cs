namespace HMailServer.Core.Abstractions;

public interface IRemoteSmtpClient
{
    ValueTask<RemoteSmtpSendResult> SendAsync(
        RemoteSmtpSendRequest request,
        CancellationToken cancellationToken);
}
