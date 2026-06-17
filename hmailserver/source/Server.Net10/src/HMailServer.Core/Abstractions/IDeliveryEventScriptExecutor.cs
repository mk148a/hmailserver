namespace HMailServer.Core.Abstractions;

public interface IDeliveryEventScriptExecutor
{
    DeliveryEventScriptExecutionResult Execute(
        DeliveryEventScriptExecutionRequest request,
        CancellationToken cancellationToken);
}
