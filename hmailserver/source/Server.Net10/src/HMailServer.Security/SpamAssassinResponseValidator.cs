namespace HMailServer.Security;

public static class SpamAssassinResponseValidator
{
    public static bool TryReadContentLength(ReadOnlySpan<char> header, out int contentLength)
    {
        contentLength = 0;
        var lines = header.ToString().Split("\r\n", StringSplitOptions.None);
        if (lines.Length < 2 || !StringComparer.Ordinal.Equals(lines[0], "SPAMD/1.1 0 EX_OK"))
        {
            return false;
        }

        const string prefix = "Content-length:";
        foreach (var line in lines.Skip(1))
        {
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[prefix.Length..].Trim();
            return int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out contentLength)
                && contentLength > 0;
        }

        return false;
    }
}
