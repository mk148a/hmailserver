namespace HMailServer.Core.Abstractions;

public interface IErrorEventScriptExecutor
{
    void Execute(
        ErrorEventScriptExecutionRequest request,
        CancellationToken cancellationToken);
}
