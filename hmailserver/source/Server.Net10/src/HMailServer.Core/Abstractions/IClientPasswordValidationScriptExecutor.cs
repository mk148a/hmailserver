namespace HMailServer.Core.Abstractions;

public interface IClientPasswordValidationScriptExecutor
{
    ClientPasswordValidationScriptResult Execute(
        ClientPasswordValidationScriptRequest request,
        CancellationToken cancellationToken);
}
