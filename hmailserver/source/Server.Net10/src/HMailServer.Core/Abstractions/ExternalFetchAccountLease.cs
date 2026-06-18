namespace HMailServer.Core.Abstractions;

public sealed record ExternalFetchAccountLease(
    int FetchAccountId,
    int AccountId,
    string Name,
    string ServerAddress,
    int ServerPort,
    ExternalFetchServerType ServerType,
    string Username,
    string Password,
    int MinutesBetweenFetch,
    int DaysToKeep,
    bool ProcessMimeRecipients,
    bool ProcessMimeDate,
    ExternalFetchConnectionSecurity ConnectionSecurity,
    bool UseAntiSpam,
    bool UseAntiVirus,
    bool EnableRouteRecipients,
    string MimeRecipientHeaders);
