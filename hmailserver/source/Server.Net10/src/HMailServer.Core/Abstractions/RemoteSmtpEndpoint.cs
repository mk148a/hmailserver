namespace HMailServer.Core.Abstractions;

public sealed record RemoteSmtpEndpoint(
    string Host,
    int Port,
    RemoteSmtpConnectionSecurity ConnectionSecurity,
    bool RequiresAuthentication = false,
    string AuthenticationUsername = "",
    string AuthenticationPassword = "");
