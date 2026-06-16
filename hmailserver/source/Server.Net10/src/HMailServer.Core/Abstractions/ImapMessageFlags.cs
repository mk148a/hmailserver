namespace HMailServer.Core.Abstractions;

public static class ImapMessageFlags
{
    public const byte Seen = 1;
    public const byte Deleted = 2;
    public const byte Flagged = 4;
    public const byte Answered = 8;
    public const byte Draft = 16;
    public const byte Recent = 32;
}
