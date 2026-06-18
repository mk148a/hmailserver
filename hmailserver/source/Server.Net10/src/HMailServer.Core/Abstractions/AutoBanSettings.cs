namespace HMailServer.Core.Abstractions;

public sealed record AutoBanSettings(
    bool Enabled,
    int MaxInvalidLogonAttempts,
    int LogonAttemptsWithinMinutes,
    int AutoBanMinutes);
