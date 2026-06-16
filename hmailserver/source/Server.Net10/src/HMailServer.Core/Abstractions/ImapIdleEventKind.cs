namespace HMailServer.Core.Abstractions;

public enum ImapIdleEventKind
{
    Exists,
    Recent,
    Expunge,
    FetchFlags
}
