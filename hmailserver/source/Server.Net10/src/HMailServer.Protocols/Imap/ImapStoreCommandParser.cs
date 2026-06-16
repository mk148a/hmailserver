using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapStoreCommandParser
{
    public ImapStoreRequest Parse(
        int accountId,
        int folderId,
        string arguments,
        bool useUid)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var tokens = Tokenize(arguments);
        if (tokens.Count < 2)
        {
            throw new ImapStoreParseException("STORE requires a message set, mode, and flag list.");
        }

        var index = 0;
        var messageSet = ImapSequenceSetParser.Parse(
            ReadRequiredAtom(tokens, ref index, "message set"),
            "STORE",
            "STORE",
            "STORE message set",
            static message => new ImapStoreParseException(message));
        var mode = ParseMode(ReadRequiredAtom(tokens, ref index, "STORE mode"), out var silent);
        var flags = ReadFlags(tokens, ref index);

        if (index != tokens.Count)
        {
            throw new ImapStoreParseException("Unexpected token after STORE flag list.");
        }

        return new ImapStoreRequest(
            accountId,
            folderId,
            messageSet,
            useUid,
            mode,
            flags,
            silent);
    }

    private static ImapStoreMode ParseMode(string value, out bool silent)
    {
        silent = value.EndsWith(".SILENT", StringComparison.OrdinalIgnoreCase);
        var mode = silent ? value[..^7] : value;
        return mode.ToUpperInvariant() switch
        {
            "FLAGS" => ImapStoreMode.Set,
            "+FLAGS" => ImapStoreMode.Add,
            "-FLAGS" => ImapStoreMode.Remove,
            _ => throw new ImapStoreParseException($"Unsupported STORE mode '{value}'.")
        };
    }

    private static byte ReadFlags(IReadOnlyList<Token> tokens, ref int index)
    {
        if (index >= tokens.Count)
        {
            throw new ImapStoreParseException("Missing STORE flag list.");
        }

        byte flags = 0;
        if (tokens[index].Kind == TokenKind.OpenParenthesis)
        {
            index++;
            while (index < tokens.Count && tokens[index].Kind != TokenKind.CloseParenthesis)
            {
                flags = AddFlag(flags, ReadRequiredAtom(tokens, ref index, "STORE flag"));
            }

            if (index >= tokens.Count || tokens[index].Kind != TokenKind.CloseParenthesis)
            {
                throw new ImapStoreParseException("STORE flag list is not terminated.");
            }

            index++;
            return flags;
        }

        return AddFlag(flags, ReadRequiredAtom(tokens, ref index, "STORE flag"));
    }

    private static byte AddFlag(byte flags, string value)
    {
        var flag = value.ToUpperInvariant() switch
        {
            "\\SEEN" => ImapMessageFlags.Seen,
            "\\DELETED" => ImapMessageFlags.Deleted,
            "\\DRAFT" => ImapMessageFlags.Draft,
            "\\ANSWERED" => ImapMessageFlags.Answered,
            "\\FLAGGED" => ImapMessageFlags.Flagged,
            _ => throw new ImapStoreParseException($"Unsupported STORE flag '{value}'.")
        };

        return (byte)(flags | flag);
    }

    private static string ReadRequiredAtom(IReadOnlyList<Token> tokens, ref int index, string description)
    {
        if (index >= tokens.Count || tokens[index].Kind != TokenKind.Atom)
        {
            throw new ImapStoreParseException($"Missing {description}.");
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
