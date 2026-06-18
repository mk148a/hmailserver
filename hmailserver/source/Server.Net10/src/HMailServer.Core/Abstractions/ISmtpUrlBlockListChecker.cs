namespace HMailServer.Core.Abstractions;

public interface ISmtpUrlBlockListChecker
{
    ValueTask<SmtpUrlBlockListResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken);
}
