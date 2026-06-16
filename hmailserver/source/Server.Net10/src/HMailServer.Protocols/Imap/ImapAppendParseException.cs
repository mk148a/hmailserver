namespace HMailServer.Protocols.Imap;

public sealed class ImapAppendParseException : Exception
{
    public ImapAppendParseException(string message)
        : base(message)
    {
    }
}
