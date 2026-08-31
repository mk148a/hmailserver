namespace HMailServer.Protocols.Imap;

public sealed class ImapSearchCommandHandler
{
    private readonly ImapSearchCommandParser _parser;
    private readonly ImapSearchExecutor _executor;

    public ImapSearchCommandHandler(
        ImapSearchCommandParser parser,
        ImapSearchExecutor executor)
    {
        _parser = parser;
        _executor = executor;
    }

    public async ValueTask<string> HandleAsync(
        int accountId,
        int folderId,
        string tag,
        string commandText,
        CancellationToken cancellationToken,
        IReadOnlySet<long>? sessionRecentUids = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(commandText);

        try
        {
            var request = _parser.ParseCriteria(
                accountId,
                folderId,
                commandText,
                returnUid: false);
            if (sessionRecentUids is not null)
            {
                request = request with { SessionRecentUids = sessionRecentUids };
            }

            var searchResponse = await _executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return searchResponse + $"{SanitizeAtom(tag)} OK Search completed\r\n";
        }
        catch (ImapSearchParseException ex)
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
