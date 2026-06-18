namespace HMailServer.Core.Abstractions;

public enum ExternalFetchConnectionSecurity
{
    None = 0,
    Ssl = 1,
    StartTlsOptional = 2,
    StartTlsRequired = 3
}
