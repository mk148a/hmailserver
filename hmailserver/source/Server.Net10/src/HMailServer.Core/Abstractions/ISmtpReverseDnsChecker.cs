namespace HMailServer.Core.Abstractions;

public interface ISmtpReverseDnsChecker
{
    ValueTask<SmtpReverseDnsResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken);
}
