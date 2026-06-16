using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapFetchCommandHandler
{
    private readonly ImapFetchCommandParser _parser;
    private readonly IImapMessageFetchStore _fetchStore;

    public ImapFetchCommandHandler(
        ImapFetchCommandParser parser,
        IImapMessageFetchStore fetchStore)
    {
        _parser = parser;
        _fetchStore = fetchStore;
    }

    public async ValueTask<byte[]> HandleAsync(
        int accountId,
        int folderId,
        string tag,
        string arguments,
        bool useUid,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            var request = _parser.Parse(accountId, folderId, arguments, useUid);
            var messages = new List<ImapFetchedMessage>();
            await foreach (var message in _fetchStore.FetchAsync(request, cancellationToken).ConfigureAwait(false))
            {
                messages.Add(message);
            }

            return ImapFetchResponseFormatter.Format(messages, request.Items, tag);
        }
        catch (ImapFetchParseException ex)
        {
            return Encode($"{SanitizeAtom(tag)} BAD {SanitizeResponseText(ex.Message)}\r\n");
        }
        catch (InvalidOperationException ex)
        {
            return Encode($"{SanitizeAtom(tag)} NO {SanitizeResponseText(ex.Message)}\r\n");
        }
        catch (IOException ex)
        {
            return Encode($"{SanitizeAtom(tag)} NO {SanitizeResponseText(ex.Message)}\r\n");
        }
    }

    private static byte[] Encode(string value) => System.Text.Encoding.ASCII.GetBytes(value);

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
