namespace HMailServer.Protocols.Imap;

public sealed record ImapSessionOptions
{
    public const int DefaultMaxLineBytes = 8192;

    public int MaxLineBytes { get; init; } = DefaultMaxLineBytes;

    public bool RequireTlsForAuthentication { get; init; }

    public bool ImapSaslPlainEnabled { get; init; } = true;

    public Func<CancellationToken, ValueTask<bool>>? ImapSaslPlainEnabledProvider { get; init; }

    public string Greeting { get; init; } = "* OK hMailServer .NET 10 IMAP ready\r\n";
}
