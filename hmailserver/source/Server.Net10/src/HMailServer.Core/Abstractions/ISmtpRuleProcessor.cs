namespace HMailServer.Core.Abstractions;

public interface ISmtpRuleProcessor
{
    ValueTask<SmtpRuleProcessingResult> ProcessAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken);
}
