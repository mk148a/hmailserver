using System.Globalization;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapStoreCommandHandler
{
    private readonly ImapStoreCommandParser _parser;
    private readonly IImapMessageMutationStore _mutationStore;

    public ImapStoreCommandHandler(
        ImapStoreCommandParser parser,
        IImapMessageMutationStore mutationStore)
    {
        _parser = parser;
        _mutationStore = mutationStore;
    }

    public async ValueTask<string> HandleAsync(
        int accountId,
        int folderId,
        string tag,
        string arguments,
        bool useUid,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(arguments);

        ImapStoreRequest request;
        try
        {
            request = _parser.Parse(accountId, folderId, arguments, useUid);
        }
        catch (ImapStoreParseException ex)
        {
            return $"{SanitizeAtom(tag)} BAD {SanitizeResponseText(ex.Message)}\r\n";
        }

        var builder = new StringBuilder();
        await foreach (var message in _mutationStore.StoreFlagsAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (request.Silent)
            {
                continue;
            }

            builder.Append("* ")
                .Append(message.SequenceNumber.ToString(CultureInfo.InvariantCulture))
                .Append(" FETCH (FLAGS ")
                .Append(ImapFetchResponseFormatter.FormatFlags(message.Flags))
                .Append(" UID ")
                .Append(message.Identity.Uid.ToString(CultureInfo.InvariantCulture))
                .Append(")\r\n");
        }

        builder.Append(SanitizeAtom(tag)).Append(" OK STORE completed\r\n");
        return builder.ToString();
    }

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
