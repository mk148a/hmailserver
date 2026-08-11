namespace HMailServer.Core.Abstractions;

public sealed record DeliveryTarget(
    DeliveryTargetKind Kind,
    string Key,
    string DomainName,
    int LocalAccountId = 0,
    SmtpRouteResolution? Route = null,
    int RemoteConnectionSecurity = 0);
