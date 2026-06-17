namespace HMailServer.Core.Abstractions;

public interface ISmtpEventScriptExecutor
{
    SmtpRuleScriptExecutionResult Execute(
        SmtpEventScriptExecutionRequest request,
        CancellationToken cancellationToken);
}
