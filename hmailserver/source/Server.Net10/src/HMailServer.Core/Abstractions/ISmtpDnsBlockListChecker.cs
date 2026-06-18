namespace HMailServer.Core.Abstractions;

public interface ISmtpDnsBlockListChecker
{
    ValueTask<SmtpDnsBlockListResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken);
}
