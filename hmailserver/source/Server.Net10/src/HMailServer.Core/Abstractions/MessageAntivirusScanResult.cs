namespace HMailServer.Core.Abstractions;

public sealed record MessageAntivirusScanResult(
    bool Succeeded,
    bool IsInfected,
    string Details,
    string VirusName)
{
    public static MessageAntivirusScanResult Clean(string details = "") =>
        new(
            Succeeded: true,
            IsInfected: false,
            details,
            VirusName: string.Empty);

    public static MessageAntivirusScanResult Infected(
        string virusName,
        string details = "") =>
        new(
            Succeeded: true,
            IsInfected: true,
            details,
            virusName);

    public static MessageAntivirusScanResult Error(string details) =>
        new(
            Succeeded: false,
            IsInfected: false,
            details,
            VirusName: string.Empty);
}
