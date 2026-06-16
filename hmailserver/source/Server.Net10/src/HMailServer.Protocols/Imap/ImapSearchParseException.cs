namespace HMailServer.Protocols.Imap;

public sealed class ImapSearchParseException : FormatException
{
    public ImapSearchParseException(string message)
        : base(message)
    {
    }
}
