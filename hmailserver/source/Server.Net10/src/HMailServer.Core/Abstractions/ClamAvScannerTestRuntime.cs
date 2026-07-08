namespace HMailServer.Core.Abstractions;

public sealed record ClamAvScannerTestResult(
    bool Succeeded,
    string ResultText);

public interface IClamAvScannerTestRuntime
{
    ClamAvScannerTestResult TestConnection(
        string hostname,
        int port);
}
