namespace HMailServer.Core.Abstractions;

public interface IEmailAllAccountsRuntime
{
    ValueTask<bool> EmailAllAccountsAsync(
        string recipientWildcard,
        string fromAddress,
        string fromName,
        string subject,
        string body,
        CancellationToken cancellationToken);
}
