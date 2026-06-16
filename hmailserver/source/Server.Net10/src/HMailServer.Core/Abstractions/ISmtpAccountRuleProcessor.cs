namespace HMailServer.Core.Abstractions;

public interface ISmtpAccountRuleProcessor
{
    ValueTask<SmtpRuleProcessingResult> ProcessAccountAsync(
        int accountId,
        SmtpReceiveRequest request,
        CancellationToken cancellationToken);
}
