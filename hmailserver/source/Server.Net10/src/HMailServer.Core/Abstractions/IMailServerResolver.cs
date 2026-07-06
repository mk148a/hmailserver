namespace HMailServer.Core.Abstractions;

public interface IMailServerResolver
{
    string GetMailServer(string emailAddress);
}
