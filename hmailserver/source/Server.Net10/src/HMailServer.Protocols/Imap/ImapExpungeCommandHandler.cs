using System.Globalization;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapExpungeCommandHandler
{
    private readonly IImapMessageMutationStore _mutationStore;

    public ImapExpungeCommandHandler(IImapMessageMutationStore mutationStore)
    {
        _mutationStore = mutationStore;
    }

    public async ValueTask<string> HandleAsync(
        int accountId,
        int folderId,
        string tag,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var builder = new StringBuilder();
        await foreach (var message in _mutationStore.ExpungeDeletedAsync(accountId, folderId, cancellationToken).ConfigureAwait(false))
        {
            builder.Append("* ")
                .Append(message.SequenceNumber.ToString(CultureInfo.InvariantCulture))
                .Append(" EXPUNGE\r\n");
        }

        builder.Append(SanitizeAtom(tag)).Append(" OK EXPUNGE completed\r\n");
        return builder.ToString();
    }

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
}
