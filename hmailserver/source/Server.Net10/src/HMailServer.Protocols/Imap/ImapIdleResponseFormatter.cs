using System.Globalization;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public static class ImapIdleResponseFormatter
{
    public static string Format(ImapIdleEvent idleEvent)
    {
        ArgumentNullException.ThrowIfNull(idleEvent);

        return idleEvent.Kind switch
        {
            ImapIdleEventKind.Exists => FormatNumber(idleEvent.Number, "EXISTS"),
            ImapIdleEventKind.Recent => FormatNumber(idleEvent.Number, "RECENT"),
            ImapIdleEventKind.Expunge => FormatNumber(idleEvent.Number, "EXPUNGE"),
            ImapIdleEventKind.FetchFlags => FormatFetchFlags(idleEvent),
            _ => throw new ArgumentOutOfRangeException(nameof(idleEvent), idleEvent.Kind, "Unknown IMAP IDLE event kind.")
        };
    }

    private static string FormatNumber(long number, string name) =>
        "* " + number.ToString(CultureInfo.InvariantCulture) + " " + name + "\r\n";

    private static string FormatFetchFlags(ImapIdleEvent idleEvent)
    {
        if (idleEvent.Flags is null)
        {
            throw new InvalidOperationException("FETCH FLAGS IDLE events require flags.");
        }

        var response = "* " +
            idleEvent.Number.ToString(CultureInfo.InvariantCulture) +
            " FETCH (FLAGS " +
            ImapFetchResponseFormatter.FormatFlags(idleEvent.Flags.Value);

        if (idleEvent.Uid is { } uid)
        {
            response += " UID " + uid.ToString(CultureInfo.InvariantCulture);
        }

        return response + ")\r\n";
    }
}
