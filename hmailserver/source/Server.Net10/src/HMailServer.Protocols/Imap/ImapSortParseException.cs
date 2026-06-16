namespace HMailServer.Protocols.Imap;

public sealed class ImapSortParseException : Exception
{
    public ImapSortParseException(string message)
        : base(message)
    {
    }
}
