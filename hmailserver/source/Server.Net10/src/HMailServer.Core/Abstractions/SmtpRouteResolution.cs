namespace HMailServer.Core.Abstractions;

public sealed record SmtpRouteResolution(
    int RouteId,
    string DomainName,
    string TargetHost,
    int TargetPort,
    int ConnectionSecurity,
    bool TreatRecipientAsLocal,
    bool RequiresAuthentication = false,
    string AuthenticationUsername = "",
    string AuthenticationPassword = "");
