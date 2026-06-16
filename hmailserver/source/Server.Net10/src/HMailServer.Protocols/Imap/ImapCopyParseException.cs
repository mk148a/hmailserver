namespace HMailServer.Protocols.Imap;

public sealed class ImapCopyParseException : Exception
{
    public ImapCopyParseException(string message)
        : base(message)
    {
    }
}
