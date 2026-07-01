namespace HMailServer.Core.Abstractions;

public sealed record LoggingAdministrationSnapshot(
    int LoggingMask,
    int Device,
    int LogFormat,
    bool AwStatsEnabled);
