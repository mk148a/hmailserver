namespace HMailServer.Protocols.Imap;

public static class ImapCommandArguments
{
    public static bool IsFirstArgumentQuoted(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var index = 0;
        while (index < arguments.Length && char.IsWhiteSpace(arguments[index]))
        {
            index++;
        }

        return index < arguments.Length && arguments[index] == '"';
    }

    public static IReadOnlyList<string> Parse(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var values = new List<string>();
        var index = 0;
        while (index < arguments.Length)
        {
            while (index < arguments.Length && char.IsWhiteSpace(arguments[index]))
            {
                index++;
            }

            if (index >= arguments.Length)
            {
                break;
            }

            values.Add(arguments[index] == '"'
                ? ReadQuoted(arguments, ref index)
                : ReadAtom(arguments, ref index));
        }

        return values;
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
                    throw new ImapSearchParseException("Quoted string ends with an escape character.");
                }

                builder.Append(value[index++]);
                continue;
            }

            builder.Append(current);
        }

        throw new ImapSearchParseException("Quoted string is not terminated.");
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
}
