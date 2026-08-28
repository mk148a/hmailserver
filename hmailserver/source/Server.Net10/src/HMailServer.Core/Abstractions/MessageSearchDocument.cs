namespace HMailServer.Core.Abstractions;

public sealed record MessageSearchDocument(
    MessageIdentity Identity,
    DateTimeOffset InternalDateUtc,
    long SizeBytes,
    byte Flags,
    string HeaderText,
    string BodyText,
    string CombinedText)
{
    public string SubjectText { get; init; } = string.Empty;

    public string FileSearchHeaderText { get; init; } = string.Empty;

    public string FileSearchPlainBodyText { get; init; } = string.Empty;

    public string FileSearchHtmlBodyText { get; init; } = string.Empty;
}
