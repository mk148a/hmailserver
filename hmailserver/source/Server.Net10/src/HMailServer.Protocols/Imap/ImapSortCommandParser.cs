using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapSortCommandParser
{
    private readonly ImapSearchCommandParser _searchParser;

    public ImapSortCommandParser(ImapSearchCommandParser searchParser)
    {
        _searchParser = searchParser;
    }

    public ImapSortRequest Parse(
        int accountId,
        int folderId,
        string arguments,
        bool returnUid)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var index = 0;
        SkipWhitespace(arguments, ref index);
        if (TryReadAtom(arguments, ref index, out var commandName) &&
            commandName.Equals("SORT", StringComparison.OrdinalIgnoreCase))
        {
            SkipWhitespace(arguments, ref index);
        }
        else
        {
            index = 0;
        }

        var criteria = ReadSortCriteria(arguments, ref index);
        SkipWhitespace(arguments, ref index);
        var charset = ReadAtom(arguments, ref index, "SORT charset");
        if (!IsSupportedCharset(charset))
        {
            throw new ImapSortParseException($"Unsupported SORT CHARSET '{charset}'.");
        }

        var searchCriteria = arguments[index..].Trim();
        if (searchCriteria.Length == 0)
        {
            searchCriteria = "ALL";
        }

        try
        {
            var searchRequest = _searchParser.ParseCriteria(
                accountId,
                folderId,
                searchCriteria,
                returnUid);
            return new ImapSortRequest(searchRequest, criteria);
        }
        catch (ImapSearchParseException ex)
        {
            throw new ImapSortParseException(ex.Message);
        }
    }

    private static IReadOnlyList<ImapSortCriterion> ReadSortCriteria(string value, ref int index)
    {
        SkipWhitespace(value, ref index);
        if (index >= value.Length || value[index] != '(')
        {
            throw new ImapSortParseException("SORT requires a parenthesized sort criteria list.");
        }

        index++;
        var criteria = new List<ImapSortCriterion>();
        var reverseNext = false;
        while (index < value.Length)
        {
            SkipWhitespace(value, ref index);
            if (index < value.Length && value[index] == ')')
            {
                index++;
                if (reverseNext)
                {
                    throw new ImapSortParseException("SORT REVERSE must be followed by a sort criterion.");
                }

                if (criteria.Count == 0)
                {
                    throw new ImapSortParseException("SORT criteria list is empty.");
                }

                return criteria;
            }

            var token = ReadAtom(value, ref index, "SORT criterion");
            if (token.Equals("REVERSE", StringComparison.OrdinalIgnoreCase))
            {
                if (reverseNext)
                {
                    throw new ImapSortParseException("SORT REVERSE cannot be repeated without a criterion.");
                }

                reverseNext = true;
                continue;
            }

            criteria.Add(new ImapSortCriterion(ParseSortKey(token), reverseNext));
            reverseNext = false;
        }

        throw new ImapSortParseException("SORT criteria list is not terminated.");
    }

    private static ImapSortKey ParseSortKey(string value) =>
        value.ToUpperInvariant() switch
        {
            "ARRIVAL" => ImapSortKey.Arrival,
            "CC" => ImapSortKey.Cc,
            "DATE" => ImapSortKey.Date,
            "FROM" => ImapSortKey.From,
            "SIZE" => ImapSortKey.Size,
            "SUBJECT" => ImapSortKey.Subject,
            "TO" => ImapSortKey.To,
            _ => throw new ImapSortParseException($"Unsupported SORT criterion '{value}'.")
        };

    private static string ReadAtom(string value, ref int index, string description)
    {
        if (!TryReadAtom(value, ref index, out var atom))
        {
            throw new ImapSortParseException($"Missing {description}.");
        }

        return atom;
    }

    private static bool TryReadAtom(string value, ref int index, out string atom)
    {
        SkipWhitespace(value, ref index);
        var start = index;
        while (index < value.Length && !char.IsWhiteSpace(value[index]) && value[index] is not '(' and not ')')
        {
            index++;
        }

        atom = value[start..index];
        return atom.Length > 0;
    }

    private static bool IsSupportedCharset(string charset) =>
        charset.Equals("US-ASCII", StringComparison.OrdinalIgnoreCase) ||
        charset.Equals("UTF-8", StringComparison.OrdinalIgnoreCase);

    private static void SkipWhitespace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }
}
