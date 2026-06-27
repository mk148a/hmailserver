namespace HMailServer.Core.Abstractions;

public sealed record RouteAdministrationSnapshot(
    int Id,
    string DomainName,
    string Description,
    string TargetSmtpHost,
    int TargetSmtpPort,
    int NumberOfTries,
    int MinutesBetweenTry,
    bool AllAddresses,
    bool RelayerRequiresAuth,
    string RelayerAuthUsername,
    bool TreatRecipientAsLocalDomain,
    bool TreatSenderAsLocalDomain,
    int ConnectionSecurity);
