namespace HMailServer.Core.Abstractions;

public sealed record ClamWinScannerTestResult(
    bool Succeeded,
    string ResultText);

public interface IClamWinScannerTestRuntime
{
    ClamWinScannerTestResult TestConnection(
        string executablePath,
        string databasePath);
}
