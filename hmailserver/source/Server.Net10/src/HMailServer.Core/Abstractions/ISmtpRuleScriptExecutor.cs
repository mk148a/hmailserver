namespace HMailServer.Core.Abstractions;

public interface ISmtpRuleScriptExecutor
{
    SmtpRuleScriptExecutionResult Execute(
        SmtpRuleScriptExecutionRequest request,
        CancellationToken cancellationToken);
}
