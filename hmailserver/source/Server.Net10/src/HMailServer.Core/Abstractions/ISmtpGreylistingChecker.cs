namespace HMailServer.Core.Abstractions;

public interface ISmtpGreylistingChecker
{
    ValueTask<SmtpGreylistingResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken);
}
