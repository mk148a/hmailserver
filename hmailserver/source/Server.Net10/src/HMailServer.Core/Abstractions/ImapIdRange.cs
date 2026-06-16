namespace HMailServer.Core.Abstractions;

public readonly record struct ImapIdRange(long? Start, long? End)
{
    public bool IsSingle => Start is not null && End == Start;
}
