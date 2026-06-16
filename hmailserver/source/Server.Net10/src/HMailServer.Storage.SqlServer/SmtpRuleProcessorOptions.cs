namespace HMailServer.Storage.SqlServer;

public sealed record SmtpRuleProcessorOptions
{
    public int RuleLoopLimit { get; init; } = 5;
}
