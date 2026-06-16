namespace HMailServer.Core.Abstractions;

public interface ISmtpMessageReceiver
{
    ValueTask<SmtpReceiveResult> ReceiveAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken);
}
