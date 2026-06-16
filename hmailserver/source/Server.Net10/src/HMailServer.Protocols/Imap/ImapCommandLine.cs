namespace HMailServer.Protocols.Imap;

public sealed record ImapCommandLine(
    string Tag,
    string Command,
    string Arguments,
    bool IsUidCommand)
{
    public static bool TryParse(string line, out ImapCommandLine commandLine)
    {
        commandLine = default!;

        if (!TryReadAtom(line, 0, out var tag, out var nextIndex))
        {
            return false;
        }

        if (!TryReadAtom(line, nextIndex, out var command, out nextIndex))
        {
            return false;
        }

        var isUidCommand = command.Equals("UID", StringComparison.OrdinalIgnoreCase);
        if (isUidCommand)
        {
            if (!TryReadAtom(line, nextIndex, out command, out nextIndex))
            {
                return false;
            }
        }

        var arguments = nextIndex >= line.Length ? string.Empty : line[nextIndex..].TrimStart();
        commandLine = new ImapCommandLine(
            tag,
            command.ToUpperInvariant(),
            arguments,
            isUidCommand);
        return true;
    }

    private static bool TryReadAtom(
        string line,
        int startIndex,
        out string atom,
        out int nextIndex)
    {
        atom = string.Empty;
        nextIndex = startIndex;

        while (nextIndex < line.Length && char.IsWhiteSpace(line[nextIndex]))
        {
            nextIndex++;
        }

        if (nextIndex >= line.Length)
        {
            return false;
        }

        var atomStart = nextIndex;
        while (nextIndex < line.Length && !char.IsWhiteSpace(line[nextIndex]))
        {
            nextIndex++;
        }

        atom = line[atomStart..nextIndex];
        return atom.Length > 0;
    }
}
