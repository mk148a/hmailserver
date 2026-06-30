namespace HMailServer.Core.Abstractions;

public sealed record SettingsAdministrationSnapshot(
    string HostName,
    string WelcomeSmtp,
    string WelcomePop3,
    string WelcomeImap);
