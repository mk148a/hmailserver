namespace HMailServer.Protocols.Imap;

public interface IImapSessionContextProvider
{
    ValueTask<ImapSessionContext> GetContextAsync(CancellationToken cancellationToken);
}
