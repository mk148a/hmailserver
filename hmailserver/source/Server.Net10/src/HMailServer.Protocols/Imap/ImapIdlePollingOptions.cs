namespace HMailServer.Protocols.Imap;

public sealed class ImapIdlePollingOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);
}
