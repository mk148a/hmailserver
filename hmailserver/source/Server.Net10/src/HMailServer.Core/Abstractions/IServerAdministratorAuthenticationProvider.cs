namespace HMailServer.Core.Abstractions;

public interface IServerAdministratorAuthenticationProvider
{
    bool Authenticate(string username, string password);
}
