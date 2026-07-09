namespace HMailServer.Core.Abstractions;

public sealed record CustomScannerTestResult(
    bool Succeeded,
    string ResultText);

public interface ICustomScannerTestRuntime
{
    CustomScannerTestResult TestConnection(
        string commandLineTemplate,
        int virusReturnCode);
}
