namespace HMailServer.Core.Abstractions;

public sealed record SettingsAdministrationSnapshot(
    string HostName,
    string WelcomeSmtp,
    string WelcomePop3,
    string WelcomeImap,
    int MaxSmtpConnections = 0,
    int MaxPop3Connections = 0,
    int MaxImapConnections = 0,
    int MaxDeliveryThreads = 0,
    bool ServiceSmtp = false,
    bool ServicePop3 = false,
    bool ServiceImap = false,
    int SmtpNoOfTries = 0,
    int SmtpMinutesBetweenTry = 0,
    int MaxMessageSize = 0,
    int MaxSmtpRecipientsInBatch = 0,
    bool DisconnectInvalidClients = false,
    int MaxNumberOfInvalidCommands = 0);
