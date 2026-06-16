using System.Globalization;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapAppendCommandParser
{
    public ImapAppendCommand Parse(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var trimmed = arguments.Trim();
        var literalStart = trimmed.LastIndexOf('{');
        if (literalStart < 0 || !trimmed.EndsWith('}'))
        {
            throw new ImapAppendParseException("APPEND requires a synchronizing literal byte count.");
        }

        var literalText = trimmed[(literalStart + 1)..^1];
        if (literalText.EndsWith('+'))
        {
            literalText = literalText[..^1];
        }

        if (!int.TryParse(literalText, NumberStyles.None, CultureInfo.InvariantCulture, out var byteCount) || byteCount < 0)
        {
            throw new ImapAppendParseException("APPEND literal byte count is invalid.");
        }

        var prefix = trimmed[..literalStart].TrimEnd();
        var index = 0;
        var mailboxName = ReadValue(prefix, ref index, "mailbox name");
        SkipWhitespace(prefix, ref index);

        byte flags = 0;
        DateTimeOffset? internalDate = null;
        if (index < prefix.Length && prefix[index] == '(')
        {
            flags = ReadFlagList(prefix, ref index);
            SkipWhitespace(prefix, ref index);
        }

        if (index < prefix.Length)
        {
            var dateText = ReadValue(prefix, ref index, "internal date");
            if (!DateTimeOffset.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate))
            {
                throw new ImapAppendParseException("APPEND internal date is invalid.");
            }

            internalDate = parsedDate.ToUniversalTime();
            SkipWhitespace(prefix, ref index);
        }

        if (index < prefix.Length)
        {
            throw new ImapAppendParseException("Unexpected token before APPEND literal.");
        }

        return new ImapAppendCommand(mailboxName, flags, internalDate, byteCount);
    }

    private static byte ReadFlagList(string value, ref int index)
    {
        index++;
        var start = index;
        while (index < value.Length && value[index] != ')')
        {
            index++;
        }

        if (index >= value.Length)
        {
            throw new ImapAppendParseException("APPEND flag list is not terminated.");
        }

        var flags = ParseFlags(value[start..index]);
        index++;
        return flags;
    }

    private static byte ParseFlags(string value)
    {
        byte flags = 0;
        foreach (var token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var flag = token.ToUpperInvariant() switch
            {
                "\\SEEN" => ImapMessageFlags.Seen,
                "\\DELETED" => ImapMessageFlags.Deleted,
                "\\DRAFT" => ImapMessageFlags.Draft,
                "\\ANSWERED" => ImapMessageFlags.Answered,
                "\\FLAGGED" => ImapMessageFlags.Flagged,
                _ => throw new ImapAppendParseException($"Unsupported APPEND flag '{token}'.")
            };

            flags = (byte)(flags | flag);
        }

        return flags;
    }

    private static string ReadValue(string value, ref int index, string description)
    {
        SkipWhitespace(value, ref index);
        if (index >= value.Length)
        {
            throw new ImapAppendParseException($"Missing APPEND {description}.");
        }

        return value[index] == '"'
            ? ReadQuoted(value, ref index, description)
            : ReadAtom(value, ref index);
    }

    private static string ReadQuoted(string value, ref int index, string description)
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
                    throw new ImapAppendParseException($"Quoted APPEND {description} ends with an escape character.");
                }

                builder.Append(value[index++]);
                continue;
            }

            builder.Append(current);
        }

        throw new ImapAppendParseException($"Quoted APPEND {description} is not terminated.");
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
