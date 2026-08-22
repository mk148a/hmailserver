namespace HMailServer.Protocols.Smtp;

public sealed record SmtpSessionOptions
{
    public const int DefaultMaxLineBytes = 8192;

    public int MaxLineBytes { get; init; } = DefaultMaxLineBytes;

    public long MaxMessageBytes { get; init; } = 20L * 1024 * 1024;

    public string ServerName { get; init; } = "hMailServer";

    public string Greeting { get; init; } = "220 hMailServer .NET 10 ESMTP ready\r\n";

    public Func<string>? GreetingProvider { get; init; }

    public bool RequireTlsForAuthentication { get; init; }

    public bool DisconnectInvalidClients { get; init; }

    public int MaximumIncorrectCommands { get; init; } = 100;

    public Func<int>? CrashSimulationModeProvider { get; init; }

    public Action<int>? CrashSimulationModeExecutor { get; init; }
}
