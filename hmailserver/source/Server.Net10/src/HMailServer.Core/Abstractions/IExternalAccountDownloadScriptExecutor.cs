namespace HMailServer.Core.Abstractions;

public interface IExternalAccountDownloadScriptExecutor
{
    ExternalAccountDownloadScriptExecutionResult Execute(
        ExternalAccountDownloadScriptExecutionRequest request,
        CancellationToken cancellationToken);
}
