namespace HMailServer.Core.Abstractions;

public enum RemoteSmtpConnectionSecurity
{
    None = 0,
    Ssl = 1,
    StartTlsOptional = 2,
    StartTlsRequired = 3
}
