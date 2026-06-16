using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public sealed class ImapCopyCommandParser
{
    public ImapCopyCommand Parse(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        IReadOnlyList<string> values;
        try
        {
            values = ImapCommandArguments.Parse(arguments);
        }
        catch (ImapSearchParseException ex)
        {
            throw new ImapCopyParseException(ex.Message);
        }

        if (values.Count != 2)
        {
            throw new ImapCopyParseException("COPY/MOVE requires a message set and destination mailbox.");
        }

        return new ImapCopyCommand(
            ImapSequenceSetParser.Parse(
                values[0],
                "COPY/MOVE",
                "COPY/MOVE",
                "COPY/MOVE message set",
                static message => new ImapCopyParseException(message)),
            values[1]);
    }
}
