namespace HMailServer.Core.Abstractions;

public interface IActiveDirectoryPasswordValidator
{
    bool Validate(
        string domain,
        string username,
        string password);
}
