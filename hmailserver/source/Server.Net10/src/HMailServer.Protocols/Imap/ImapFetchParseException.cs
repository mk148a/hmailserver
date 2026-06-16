namespace HMailServer.Protocols.Imap;

public sealed class ImapFetchParseException : Exception
{
    public ImapFetchParseException(string message)
        : base(message)
    {
    }
}
