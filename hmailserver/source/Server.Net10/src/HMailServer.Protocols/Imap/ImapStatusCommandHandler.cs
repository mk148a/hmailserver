using System.Globalization;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapStatusCommandHandler
{
    private readonly ImapStatusCommandParser _parser;
    private readonly IImapMailboxDiscoveryStore _mailboxStore;

    public ImapStatusCommandHandler(
        ImapStatusCommandParser parser,
        IImapMailboxDiscoveryStore mailboxStore)
    {
        _parser = parser;
        _mailboxStore = mailboxStore;
    }

    public async ValueTask<string> HandleAsync(
        int accountId,
        string tag,
        string arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(arguments);

        ImapStatusCommand command;
        try
        {
            command = _parser.Parse(arguments);
        }
        catch (ImapFetchParseException ex)
        {
            return $"{SanitizeAtom(tag)} BAD {SanitizeResponseText(ex.Message)}\r\n";
        }

        var status = await _mailboxStore
            .GetStatusAsync(accountId, command.MailboxName, command.Items, cancellationToken)
            .ConfigureAwait(false);
        if (status is null)
        {
            return $"{SanitizeAtom(tag)} BAD Folder could not be found.\r\n";
        }

        var builder = new StringBuilder();
        builder.Append("* STATUS \"")
            .Append(EscapeQuoted(status.MailboxName))
            .Append("\" (");

        for (var index = 0; index < command.Items.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            var item = command.Items[index];
            builder.Append(FormatItemName(item))
                .Append(' ')
                .Append(status.Values[item].ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(")\r\n")
            .Append(SanitizeAtom(tag))
            .Append(" OK STATUS completed\r\n");
        return builder.ToString();
    }

    private static string FormatItemName(ImapStatusItem item) =>
        item switch
        {
            ImapStatusItem.Messages => "MESSAGES",
            ImapStatusItem.Recent => "RECENT",
            ImapStatusItem.UidNext => "UIDNEXT",
            ImapStatusItem.UidValidity => "UIDVALIDITY",
            ImapStatusItem.Unseen => "UNSEEN",
            _ => throw new ArgumentOutOfRangeException(nameof(item), item, "Unknown IMAP STATUS data item.")
        };

    private static string EscapeQuoted(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string SanitizeAtom(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string SanitizeResponseText(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
