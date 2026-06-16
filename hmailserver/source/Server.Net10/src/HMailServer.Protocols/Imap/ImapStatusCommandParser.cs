using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapStatusCommandParser
{
    public ImapStatusCommand Parse(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var index = 0;
        SkipWhitespace(arguments, ref index);
        if (index >= arguments.Length)
        {
            throw new ImapFetchParseException("STATUS requires a mailbox name.");
        }

        var mailboxName = arguments[index] == '"'
            ? ReadQuoted(arguments, ref index)
            : ReadAtom(arguments, ref index);
        if (string.IsNullOrWhiteSpace(mailboxName))
        {
            throw new ImapFetchParseException("STATUS requires a mailbox name.");
        }

        SkipWhitespace(arguments, ref index);
        if (index >= arguments.Length)
        {
            return new ImapStatusCommand(mailboxName, Array.Empty<ImapStatusItem>());
        }

        string itemText;
        if (arguments[index] == '(')
        {
            itemText = ReadParenthesized(arguments, ref index);
        }
        else
        {
            itemText = arguments[index..];
            index = arguments.Length;
        }

        SkipWhitespace(arguments, ref index);
        if (index < arguments.Length)
        {
            throw new ImapFetchParseException("Unexpected token after STATUS item list.");
        }

        var items = new List<ImapStatusItem>();
        foreach (var token in itemText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            items.Add(ParseItem(token));
        }

        return new ImapStatusCommand(mailboxName, items.ToArray());
    }

    private static ImapStatusItem ParseItem(string value) =>
        value.ToUpperInvariant() switch
        {
            "MESSAGES" => ImapStatusItem.Messages,
            "RECENT" => ImapStatusItem.Recent,
            "UIDNEXT" => ImapStatusItem.UidNext,
            "UIDVALIDITY" => ImapStatusItem.UidValidity,
            "UNSEEN" => ImapStatusItem.Unseen,
            _ => throw new ImapFetchParseException($"Unsupported STATUS data item '{value}'.")
        };

    private static string ReadParenthesized(string value, ref int index)
    {
        index++;
        var start = index;
        while (index < value.Length && value[index] != ')')
        {
            index++;
        }

        if (index >= value.Length)
        {
            throw new ImapFetchParseException("STATUS item list is not terminated.");
        }

        var result = value[start..index];
        index++;
        return result;
    }

    private static string ReadQuoted(string value, ref int index)
    {
        index++;
        var builder = new System.Text.StringBuilder();
        while (index < value.Length)
        {
            var current = value[index++];
            if (current == '"')
            {
                return builder.ToString();
            }

            if (current == '\\')
            {
                if (index >= value.Length)
                {
                    throw new ImapFetchParseException("Quoted STATUS mailbox ends with an escape character.");
                }

                builder.Append(value[index++]);
                continue;
            }

            builder.Append(current);
        }

        throw new ImapFetchParseException("Quoted STATUS mailbox is not terminated.");
    }

    private static string ReadAtom(string value, ref int index)
    {
        var start = index;
        while (index < value.Length && !char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return value[start..index];
    }

    private static void SkipWhitespace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }
}
