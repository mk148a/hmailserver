namespace HMailServer.Security;

public sealed record MessageAttachmentPolicyOptions
{
    public bool Enabled { get; init; }

    public IReadOnlyList<string> BlockedWildcards { get; init; } = [];

    public string ReplacementTextTemplate { get; init; } =
        "The attachment %MACRO_FILE% was removed because it matched an attachment blocking rule.";
}
