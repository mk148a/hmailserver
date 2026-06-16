namespace HMailServer.Protocols.Imap;

public sealed class FixedImapSessionContextProvider : IImapSessionContextProvider
{
    private readonly ImapSessionContext _context;

    public FixedImapSessionContextProvider(ImapSessionContext context)
    {
        _context = context;
    }

    public ValueTask<ImapSessionContext> GetContextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_context);
    }
}
