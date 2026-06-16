using System.Globalization;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Imap;

public static class ImapSequenceSetParser
{
    public static IReadOnlyList<ImapIdRange> Parse(
        string value,
        string bareStarCommandName,
        string bareStarRangeName,
        string emptySetName,
        Func<string, Exception> createException)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(bareStarCommandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(bareStarRangeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptySetName);
        ArgumentNullException.ThrowIfNull(createException);

        var ranges = new List<ImapIdRange>();
        foreach (var segment in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIndex = segment.IndexOf(':', StringComparison.Ordinal);
            if (colonIndex < 0)
            {
                var id = ParseOptionalStarId(segment, createException);
                if (id is null)
                {
                    throw createException($"Bare '*' {bareStarCommandName} requires mailbox high-water mark context.");
                }

                ranges.Add(new ImapIdRange(id, id));
                continue;
            }

            var left = segment[..colonIndex];
            var right = segment[(colonIndex + 1)..];
            var start = ParseOptionalStarId(left, createException);
            var end = ParseOptionalStarId(right, createException);

            if (start is null && end is null)
            {
                throw createException($"Bare '*' {bareStarRangeName} range requires mailbox high-water mark context.");
            }

            if (start is null)
            {
                ranges.Add(new ImapIdRange(end, null));
                continue;
            }

            if (end is null)
            {
                ranges.Add(new ImapIdRange(start, null));
                continue;
            }

            if (start.Value > end.Value)
            {
                (start, end) = (end, start);
            }

            ranges.Add(new ImapIdRange(start, end));
        }

        if (ranges.Count == 0)
        {
            throw createException($"{emptySetName} is empty.");
        }

        return ranges;
    }

    private static long? ParseOptionalStarId(
        string value,
        Func<string, Exception> createException)
    {
        if (value == "*")
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        throw createException($"Invalid IMAP message identifier '{value}'.");
    }
}
