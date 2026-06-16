namespace HMailServer.Protocols.Imap;

public sealed class ImapStoreParseException : Exception
{
    public ImapStoreParseException(string message)
        : base(message)
    {
    }
}
