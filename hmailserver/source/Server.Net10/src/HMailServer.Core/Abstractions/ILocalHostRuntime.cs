namespace HMailServer.Core.Abstractions;

public interface ILocalHostRuntime
{
    bool IsLocalHost(string hostName);
}
