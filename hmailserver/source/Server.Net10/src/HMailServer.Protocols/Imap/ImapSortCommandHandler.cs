namespace HMailServer.Protocols.Imap;

public sealed class ImapSortCommandHandler
{
    private readonly ImapSortCommandParser _parser;
    private readonly ImapSortExecutor _executor;

    public ImapSortCommandHandler(
        ImapSortCommandParser parser,
        ImapSortExecutor executor)
    {
        _parser = parser;
        _executor = executor;
    }

    public async ValueTask<string> HandleAsync(
        int accountId,
        int folderId,
        string tag,
        string arguments,
        bool returnUid,
        CancellationToken cancellationToken,
        IReadOnlySet<long>? sessionRecentUids = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            var request = _parser.Parse(accountId, folderId, arguments, returnUid);
            if (sessionRecentUids is not null)
            {
                request = request with
                {
                    SearchRequest = request.SearchRequest with { SessionRecentUids = sessionRecentUids }
                };
            }

            var sortResponse = await _executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return sortResponse + $"{SanitizeAtom(tag)} OK SORT completed\r\n";
        }
        catch (ImapSortParseException ex)
        {
            return $"{SanitizeAtom(tag)} BAD {SanitizeResponseText(ex.Message)}\r\n";
        }
    }

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
