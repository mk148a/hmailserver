namespace HMailServer.Security;

public sealed record ClamAvScanResult(
    bool Succeeded,
    bool IsInfected,
    string Details,
    string VirusName)
{
    public static ClamAvScanResult Clean(string details) =>
        new(
            Succeeded: true,
            IsInfected: false,
            details,
            VirusName: string.Empty);

    public static ClamAvScanResult Infected(string virusName, string details) =>
        new(
            Succeeded: true,
            IsInfected: true,
            details,
            virusName);

    public static ClamAvScanResult Error(string details) =>
        new(
            Succeeded: false,
            IsInfected: false,
            details,
            VirusName: string.Empty);
}
