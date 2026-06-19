namespace HMailServer.Core.Abstractions;

public interface ISmtpSenderDomainMxChecker
{
    ValueTask<SmtpSenderDomainMxResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken);
}
