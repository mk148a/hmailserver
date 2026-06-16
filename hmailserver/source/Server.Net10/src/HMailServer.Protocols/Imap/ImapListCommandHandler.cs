using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapListCommandHandler
{
    private readonly IImapMailboxDiscoveryStore _mailboxStore;
    private readonly string _hierarchyDelimiter;

    public ImapListCommandHandler(
        IImapMailboxDiscoveryStore mailboxStore,
        string hierarchyDelimiter)
    {
        _mailboxStore = mailboxStore;
        _hierarchyDelimiter = hierarchyDelimiter;
        ArgumentException.ThrowIfNullOrWhiteSpace(_hierarchyDelimiter);
    }

    public async ValueTask<string> HandleAsync(
        int accountId,
        string tag,
        string arguments,
        bool subscribedOnly,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(arguments);

        IReadOnlyList<string> parsedArguments;
        try
        {
            parsedArguments = ImapCommandArguments.Parse(arguments);
        }
        catch (ImapSearchParseException ex)
        {
            return $"{SanitizeAtom(tag)} BAD {SanitizeResponseText(ex.Message)}\r\n";
        }

        var commandName = subscribedOnly ? "LSUB" : "LIST";
        if (parsedArguments.Count != 2)
        {
            return $"{SanitizeAtom(tag)} BAD {commandName} requires reference name and mailbox pattern.\r\n";
        }

        var entries = new List<ImapMailboxListEntry>();
        await foreach (var entry in _mailboxStore
            .ListMailboxesAsync(accountId, parsedArguments[0], parsedArguments[1], subscribedOnly, cancellationToken)
            .ConfigureAwait(false))
        {
            entries.Add(entry);
        }

        var builder = new StringBuilder();
        if (!subscribedOnly && entries.Count == 0 && parsedArguments[1].Length == 0)
        {
            builder.Append("* LIST (\\Noselect) \"")
                .Append(EscapeQuoted(_hierarchyDelimiter))
                .Append("\" \"\"\r\n");
        }

        foreach (var entry in entries)
        {
            builder.Append("* ")
                .Append(commandName)
                .Append(" (")
                .Append(entry.HasChildren ? "\\HasChildren" : "\\HasNoChildren");

            if (!entry.IsSelectable)
            {
                builder.Append(" \\Noselect");
            }

            builder.Append(") \"")
                .Append(EscapeQuoted(_hierarchyDelimiter))
                .Append("\" \"")
                .Append(EscapeQuoted(entry.Name))
                .Append("\"\r\n");
        }

        builder.Append(SanitizeAtom(tag)).Append(" OK ").Append(commandName).Append(" completed\r\n");
        return builder.ToString();
    }

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
