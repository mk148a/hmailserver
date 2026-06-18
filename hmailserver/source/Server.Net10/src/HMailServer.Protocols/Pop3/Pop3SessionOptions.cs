namespace HMailServer.Protocols.Pop3;

public sealed record Pop3SessionOptions
{
    public const int DefaultMaxLineBytes = 8192;

    public int MaxLineBytes { get; init; } = DefaultMaxLineBytes;

    public string Greeting { get; init; } = "+OK hMailServer .NET 10 POP3 ready\r\n";
}
