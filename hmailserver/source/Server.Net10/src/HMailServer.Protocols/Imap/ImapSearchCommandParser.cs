using System.Globalization;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapSearchCommandParser
{
    private static readonly string[] DateFormats =
    [
        "d-MMM-yyyy",
        "dd-MMM-yyyy"
    ];

    public ImapSearchRequest ParseCriteria(
        int accountId,
        int folderId,
        string criteriaText,
        bool returnUid)
    {
        ArgumentNullException.ThrowIfNull(criteriaText);

        var tokens = Tokenize(criteriaText);
        var index = 0;

        if (IsAtom(tokens, index, "UID") && IsAtom(tokens, index + 1, "SEARCH"))
        {
            returnUid = true;
            index += 2;
        }
        else if (IsAtom(tokens, index, "SEARCH"))
        {
            index++;
        }

        if (IsAtom(tokens, index, "CHARSET"))
        {
            index++;
            var charset = ReadRequiredValue(tokens, ref index, "CHARSET value");
            if (!IsSupportedCharset(charset))
            {
                throw new ImapSearchParseException($"Unsupported SEARCH CHARSET '{charset}'.");
            }
        }

        byte requiredFlags = 0;
        byte forbiddenFlags = 0;
        DateOnly? since = null;
        DateOnly? before = null;
        DateOnly? sentSince = null;
        DateOnly? sentBefore = null;
        long? largerThanBytes = null;
        long? smallerThanBytes = null;
        var sequenceRanges = new List<ImapIdRange>();
        var uidRanges = new List<ImapIdRange>();
        var headerTerms = new List<string>();
        var subjectTerms = new List<string>();
        var bodyTerms = new List<string>();
        var anyTerms = new List<string>();

        while (index < tokens.Count)
        {
            if (tokens[index].Kind is TokenKind.OpenParenthesis or TokenKind.CloseParenthesis)
            {
                index++;
                continue;
            }

            var key = ReadRequiredAtom(tokens, ref index, "SEARCH key").ToUpperInvariant();
            if (LooksLikeSequenceSet(key))
            {
                foreach (var range in ImapSequenceSetParser.Parse(
                    key,
                    "SEARCH",
                    "SEARCH sequence",
                    "SEARCH sequence set",
                    static message => new ImapSearchParseException(message)))
                {
                    sequenceRanges.Add(range);
                }

                continue;
            }

            switch (key)
            {
                case "ALL":
                    break;

                case "ANSWERED":
                    AddRequiredFlag(ref requiredFlags, ImapMessageFlags.Answered);
                    break;

                case "UNANSWERED":
                    AddForbiddenFlag(ref forbiddenFlags, ImapMessageFlags.Answered);
                    break;

                case "DELETED":
                    AddRequiredFlag(ref requiredFlags, ImapMessageFlags.Deleted);
                    break;

                case "UNDELETED":
                    AddForbiddenFlag(ref forbiddenFlags, ImapMessageFlags.Deleted);
                    break;

                case "DRAFT":
                    AddRequiredFlag(ref requiredFlags, ImapMessageFlags.Draft);
                    break;

                case "UNDRAFT":
                    AddForbiddenFlag(ref forbiddenFlags, ImapMessageFlags.Draft);
                    break;

                case "FLAGGED":
                    AddRequiredFlag(ref requiredFlags, ImapMessageFlags.Flagged);
                    break;

                case "UNFLAGGED":
                    AddForbiddenFlag(ref forbiddenFlags, ImapMessageFlags.Flagged);
                    break;

                case "SEEN":
                    AddRequiredFlag(ref requiredFlags, ImapMessageFlags.Seen);
                    break;

                case "UNSEEN":
                    AddForbiddenFlag(ref forbiddenFlags, ImapMessageFlags.Seen);
                    break;

                case "RECENT":
                    AddRequiredFlag(ref requiredFlags, ImapMessageFlags.Recent);
                    break;

                case "OLD":
                    AddForbiddenFlag(ref forbiddenFlags, ImapMessageFlags.Recent);
                    break;

                case "NEW":
                    AddRequiredFlag(ref requiredFlags, ImapMessageFlags.Recent);
                    AddForbiddenFlag(ref forbiddenFlags, ImapMessageFlags.Seen);
                    break;

                case "UID":
                    foreach (var range in ImapSequenceSetParser.Parse(
                        ReadRequiredValue(tokens, ref index, "UID set"),
                        "UID SEARCH",
                        "UID",
                        "UID set",
                        static message => new ImapSearchParseException(message)))
                    {
                        uidRanges.Add(range);
                    }

                    break;

                case "SINCE":
                    since = MaxDate(since, ParseDate(ReadRequiredValue(tokens, ref index, "SINCE date")));
                    break;

                case "BEFORE":
                    before = MinDate(before, ParseDate(ReadRequiredValue(tokens, ref index, "BEFORE date")));
                    break;

                case "ON":
                    var on = ParseDate(ReadRequiredValue(tokens, ref index, "ON date"));
                    since = MaxDate(since, on);
                    before = MinDate(before, on.AddDays(1));
                    break;

                case "SENTSINCE":
                    sentSince = MaxDate(sentSince, ParseDate(ReadRequiredValue(tokens, ref index, "SENTSINCE date")));
                    break;

                case "SENTBEFORE":
                    sentBefore = MinDate(sentBefore, ParseDate(ReadRequiredValue(tokens, ref index, "SENTBEFORE date")));
                    break;

                case "SENTON":
                    var sentOn = ParseDate(ReadRequiredValue(tokens, ref index, "SENTON date"));
                    sentSince = MaxDate(sentSince, sentOn);
                    sentBefore = MinDate(sentBefore, sentOn.AddDays(1));
                    break;

                case "LARGER":
                    largerThanBytes = MaxLong(largerThanBytes, ParseNonNegativeLong(ReadRequiredValue(tokens, ref index, "LARGER size")));
                    break;

                case "SMALLER":
                    smallerThanBytes = MinLong(smallerThanBytes, ParseNonNegativeLong(ReadRequiredValue(tokens, ref index, "SMALLER size")));
                    break;

                case "BODY":
                    AddTerm(bodyTerms, ReadRequiredValue(tokens, ref index, "BODY string"));
                    break;

                case "TEXT":
                    AddTerm(anyTerms, ReadRequiredValue(tokens, ref index, "TEXT string"));
                    break;

                case "BCC":
                case "CC":
                case "FROM":
                case "TO":
                    AddTerm(headerTerms, ReadRequiredValue(tokens, ref index, $"{key} string"));
                    break;

                case "SUBJECT":
                    AddTerm(subjectTerms, ReadRequiredValue(tokens, ref index, "SUBJECT string"));
                    break;

                case "HEADER":
                    var headerName = ReadRequiredValue(tokens, ref index, "HEADER field name");
                    var headerValue = ReadRequiredValue(tokens, ref index, "HEADER value");
                    AddTerm(headerTerms, headerName);
                    AddTerm(headerTerms, headerValue);
                    break;

                default:
                    throw new ImapSearchParseException($"Unsupported SEARCH key '{key}'.");
            }
        }

        return new ImapSearchRequest(
            AccountId: accountId,
            FolderId: folderId,
            MinUid: null,
            MaxUid: null,
            RequiredFlags: requiredFlags == 0 ? null : requiredFlags,
            ForbiddenFlags: forbiddenFlags == 0 ? null : forbiddenFlags,
            Since: since,
            Before: before,
            LargerThanBytes: largerThanBytes,
            SmallerThanBytes: smallerThanBytes,
            HeaderText: null,
            BodyText: null,
            AnyText: null,
            ReturnUid: returnUid)
        {
            SequenceRanges = sequenceRanges.ToArray(),
            UidRanges = uidRanges.ToArray(),
            HeaderTerms = headerTerms.ToArray(),
            SubjectTerms = subjectTerms.ToArray(),
            BodyTerms = bodyTerms.ToArray(),
            AnyTerms = anyTerms.ToArray(),
            SentSince = sentSince,
            SentBefore = sentBefore
        };
    }

    private static void AddRequiredFlag(ref byte flags, byte flag) => flags = (byte)(flags | flag);

    private static void AddForbiddenFlag(ref byte flags, byte flag) => flags = (byte)(flags | flag);

    private static void AddTerm(List<string> terms, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            terms.Add(value.Trim());
        }
    }

    private static bool IsSupportedCharset(string charset) =>
        charset.Equals("US-ASCII", StringComparison.OrdinalIgnoreCase) ||
        charset.Equals("UTF-8", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSequenceSet(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        var hasIdentifier = false;
        foreach (var character in value)
        {
            if (char.IsDigit(character))
            {
                hasIdentifier = true;
                continue;
            }

            if (character == '*')
            {
                hasIdentifier = true;
                continue;
            }

            if (character is ':' or ',')
            {
                continue;
            }

            return false;
        }

        return hasIdentifier;
    }

    private static DateOnly ParseDate(string value)
    {
        if (DateOnly.TryParseExact(
            value,
            DateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var date))
        {
            return date;
        }

        throw new ImapSearchParseException($"Invalid IMAP SEARCH date '{value}'.");
    }

    private static long ParseNonNegativeLong(string value)
    {
        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result >= 0)
        {
            return result;
        }

        throw new ImapSearchParseException($"Invalid non-negative integer '{value}'.");
    }

    private static DateOnly? MaxDate(DateOnly? current, DateOnly candidate) =>
        current is null || candidate > current.Value ? candidate : current;

    private static DateOnly? MinDate(DateOnly? current, DateOnly candidate) =>
        current is null || candidate < current.Value ? candidate : current;

    private static long? MaxLong(long? current, long candidate) =>
        current is null || candidate > current.Value ? candidate : current;

    private static long? MinLong(long? current, long candidate) =>
        current is null || candidate < current.Value ? candidate : current;

    private static string ReadRequiredAtom(IReadOnlyList<Token> tokens, ref int index, string description)
    {
        var value = ReadRequiredValue(tokens, ref index, description);
        if (value is "(" or ")")
        {
            throw new ImapSearchParseException($"Expected {description}.");
        }

        return value;
    }

    private static string ReadRequiredValue(IReadOnlyList<Token> tokens, ref int index, string description)
    {
        if (index >= tokens.Count)
        {
            throw new ImapSearchParseException($"Missing {description}.");
        }

        return tokens[index++].Value;
    }

    private static bool IsAtom(IReadOnlyList<Token> tokens, int index, string value) =>
        index >= 0 &&
        index < tokens.Count &&
        tokens[index].Kind == TokenKind.Atom &&
        tokens[index].Value.Equals(value, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<Token> Tokenize(string value)
    {
        var tokens = new List<Token>();
        var index = 0;

        while (index < value.Length)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                index++;
                continue;
            }

            if (value[index] == '(')
            {
                tokens.Add(new Token("(", TokenKind.OpenParenthesis));
                index++;
                continue;
            }

            if (value[index] == ')')
            {
                tokens.Add(new Token(")", TokenKind.CloseParenthesis));
                index++;
                continue;
            }

            if (value[index] == '"')
            {
                tokens.Add(new Token(ReadQuotedString(value, ref index), TokenKind.QuotedString));
                continue;
            }

            tokens.Add(new Token(ReadAtom(value, ref index), TokenKind.Atom));
        }

        return tokens;
    }

    private static string ReadQuotedString(string value, ref int index)
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
                    throw new ImapSearchParseException("Quoted SEARCH string ends with an escape character.");
                }

                builder.Append(value[index++]);
                continue;
            }

            builder.Append(current);
        }

        throw new ImapSearchParseException("Quoted SEARCH string is not terminated.");
    }

    private static string ReadAtom(string value, ref int index)
    {
        var start = index;
        while (index < value.Length && !char.IsWhiteSpace(value[index]) && value[index] is not '(' and not ')')
        {
            index++;
        }

        return value[start..index];
    }

    private readonly record struct Token(string Value, TokenKind Kind);

    private enum TokenKind
    {
        Atom,
        QuotedString,
        OpenParenthesis,
        CloseParenthesis
    }
}
