namespace HMailServer.Core.Abstractions;

public sealed record ImapSearchRequest(
    int AccountId,
    int FolderId,
    long? MinUid,
    long? MaxUid,
    byte? RequiredFlags,
    byte? ForbiddenFlags,
    DateOnly? Since,
    DateOnly? Before,
    long? LargerThanBytes,
    long? SmallerThanBytes,
    string? HeaderText,
    string? BodyText,
    string? AnyText,
    bool ReturnUid)
{
    public IReadOnlyList<ImapIdRange> UidRanges { get; init; } = Array.Empty<ImapIdRange>();

    public IReadOnlyList<string> HeaderTerms { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> BodyTerms { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AnyTerms { get; init; } = Array.Empty<string>();

    public IReadOnlySet<long>? SessionRecentUids { get; init; }

    public DateOnly? SentSince { get; init; }

    public DateOnly? SentBefore { get; init; }

    public IReadOnlyList<string> GetHeaderTerms() => NormalizeTextTerms(HeaderText, HeaderTerms);

    public IReadOnlyList<string> GetBodyTerms() => NormalizeTextTerms(BodyText, BodyTerms);

    public IReadOnlyList<string> GetAnyTerms() => NormalizeTextTerms(AnyText, AnyTerms);

    private static IReadOnlyList<string> NormalizeTextTerms(string? legacyTerm, IReadOnlyList<string>? terms)
    {
        List<string>? normalized = null;
        AddTerm(legacyTerm, ref normalized);

        if (terms is not null)
        {
            foreach (var term in terms)
            {
                AddTerm(term, ref normalized);
            }
        }

        return normalized?.ToArray() ?? Array.Empty<string>();
    }

    private static void AddTerm(string? term, ref List<string>? terms)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return;
        }

        terms ??= new List<string>();
        terms.Add(term.Trim());
    }
}
