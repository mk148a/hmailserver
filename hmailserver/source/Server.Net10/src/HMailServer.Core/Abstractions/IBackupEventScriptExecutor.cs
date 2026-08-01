namespace HMailServer.Core.Abstractions;

public interface IBackupEventScriptExecutor
{
    SmtpRuleScriptExecutionResult Execute(
        BackupEventScriptExecutionRequest request,
        CancellationToken cancellationToken);
}
