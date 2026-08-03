namespace HMailServer.Core.Abstractions;

public sealed record FetchAccountAdministrationDraft(
    int AccountId,
    string Name = "",
    string ServerAddress = "",
    int Port = 0,
    int ServerType = 0,
    string Username = "",
    string Password = "",
    int MinutesBetweenFetch = 30,
    int DaysToKeepMessages = 0,
    bool Enabled = true,
    bool ProcessMimeRecipients = false,
    bool ProcessMimeDate = false,
    int ConnectionSecurity = 0,
    bool UseAntiSpam = false,
    bool UseAntiVirus = false,
    bool EnableRouteRecipients = false,
    string MimeRecipientHeaders = "");
