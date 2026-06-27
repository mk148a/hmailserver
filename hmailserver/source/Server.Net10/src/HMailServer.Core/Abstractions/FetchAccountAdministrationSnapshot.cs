namespace HMailServer.Core.Abstractions;

public sealed record FetchAccountAdministrationSnapshot(
    int Id,
    int AccountId,
    string Name,
    string ServerAddress,
    int Port,
    int ServerType,
    string Username,
    int MinutesBetweenFetch,
    int DaysToKeepMessages,
    bool Enabled,
    bool ProcessMimeRecipients,
    bool ProcessMimeDate,
    int ConnectionSecurity,
    bool UseAntiSpam,
    bool UseAntiVirus,
    bool EnableRouteRecipients,
    string MimeRecipientHeaders,
    string NextDownloadTime,
    bool IsLocked);
