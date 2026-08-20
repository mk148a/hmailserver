using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapFetchCommandParser
{
    public ImapFetchRequest Parse(
        int accountId,
        int folderId,
        string arguments,
        bool useUid)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var tokens = Tokenize(arguments);
        if (tokens.Count < 2)
        {
            throw new ImapFetchParseException("FETCH requires a message set and at least one data item.");
        }

        var index = 0;
        var messageSet = ImapSequenceSetParser.Parse(
            ReadRequiredAtom(tokens, ref index, "message set"),
            "FETCH",
            "FETCH",
            "FETCH message set",
            static message => new ImapFetchParseException(message));
        var items = ReadItems(tokens, ref index);

        if (index != tokens.Count)
        {
            throw new ImapFetchParseException("Unexpected token after FETCH item list.");
        }

        if (useUid)
        {
            AddItem(items, ImapFetchDataItem.Uid);
        }

        return new ImapFetchRequest(
            accountId,
            folderId,
            messageSet,
            useUid,
            items.ToArray());
    }

    private static List<ImapFetchDataItem> ReadItems(IReadOnlyList<Token> tokens, ref int index)
    {
        var items = new List<ImapFetchDataItem>();
        if (index >= tokens.Count)
        {
            throw new ImapFetchParseException("Missing FETCH data item list.");
        }

        if (tokens[index].Kind == TokenKind.OpenParenthesis)
        {
            index++;
            while (index < tokens.Count && tokens[index].Kind != TokenKind.CloseParenthesis)
            {
                AddFetchItem(items, ReadRequiredAtom(tokens, ref index, "FETCH data item"));
            }

            if (index >= tokens.Count || tokens[index].Kind != TokenKind.CloseParenthesis)
            {
                throw new ImapFetchParseException("FETCH data item list is not terminated.");
            }

            index++;
        }
        else
        {
            AddFetchItem(items, ReadRequiredAtom(tokens, ref index, "FETCH data item"));
        }

        if (items.Count == 0)
        {
            throw new ImapFetchParseException("FETCH data item list is empty.");
        }

        return items;
    }

    private static void AddFetchItem(List<ImapFetchDataItem> items, string value)
    {
        var item = value.ToUpperInvariant();
        switch (item)
        {
            case "FAST":
                AddItem(items, ImapFetchDataItem.Flags);
                AddItem(items, ImapFetchDataItem.InternalDate);
                AddItem(items, ImapFetchDataItem.Rfc822Size);
                break;

            case "ALL":
                AddItem(items, ImapFetchDataItem.Flags);
                AddItem(items, ImapFetchDataItem.InternalDate);
                AddItem(items, ImapFetchDataItem.Rfc822Size);
                AddItem(items, ImapFetchDataItem.Envelope);
                break;

            case "FULL":
                AddItem(items, ImapFetchDataItem.Flags);
                AddItem(items, ImapFetchDataItem.InternalDate);
                AddItem(items, ImapFetchDataItem.Rfc822Size);
                AddItem(items, ImapFetchDataItem.Envelope);
                AddItem(items, ImapFetchDataItem.BodyStructure);
                AddItem(items, ImapFetchDataItem.Body);
                break;

            case "FLAGS":
                AddItem(items, ImapFetchDataItem.Flags);
                break;

            case "UID":
                AddItem(items, ImapFetchDataItem.Uid);
                break;

            case "RFC822.SIZE":
                AddItem(items, ImapFetchDataItem.Rfc822Size);
                break;

            case "INTERNALDATE":
                AddItem(items, ImapFetchDataItem.InternalDate);
                break;

            case "ENVELOPE":
                AddItem(items, ImapFetchDataItem.Envelope);
                break;

            case "BODYSTRUCTURE":
                AddItem(items, ImapFetchDataItem.BodyStructure);
                break;

            case "BODY[]":
                AddItem(items, ImapFetchDataItem.Body);
                break;

            case "BODY.PEEK[]":
                AddItem(items, ImapFetchDataItem.BodyPeek);
                break;

            case "RFC822":
                AddItem(items, ImapFetchDataItem.Rfc822);
                break;

            default:
                throw new ImapFetchParseException($"Unsupported FETCH data item '{value}'.");
        }
    }

    private static void AddItem(List<ImapFetchDataItem> items, ImapFetchDataItem item)
    {
        if (!items.Contains(item))
        {
            items.Add(item);
        }
    }

    private static string ReadRequiredAtom(IReadOnlyList<Token> tokens, ref int index, string description)
    {
        if (index >= tokens.Count || tokens[index].Kind != TokenKind.Atom)
        {
            throw new ImapFetchParseException($"Missing {description}.");
        }

        return tokens[index++].Value;
    }

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

            tokens.Add(new Token(ReadAtom(value, ref index), TokenKind.Atom));
        }

        return tokens;
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
        OpenParenthesis,
        CloseParenthesis
    }
}
