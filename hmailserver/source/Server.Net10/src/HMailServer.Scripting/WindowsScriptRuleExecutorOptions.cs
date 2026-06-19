namespace HMailServer.Scripting;

public sealed record WindowsScriptRuleExecutorOptions
{
    public bool Enabled { get; init; }

    public string Language { get; init; } = "VBScript";

    public string EventDirectory { get; init; } = string.Empty;

    public string EventLogPath { get; init; } = string.Empty;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    public string CScriptPath { get; init; } = "cscript.exe";
}
