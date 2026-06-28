using System.Globalization;
using System.Text;

namespace HMailServer.Security;

public static class DkimSignatureParser
{
    private static readonly StringComparer TagComparer = StringComparer.OrdinalIgnoreCase;

    public static bool TryParse(
        string headerValue,
        out DkimSignature? signature,
        out string diagnostic)
    {
        ArgumentNullException.ThrowIfNull(headerValue);

        signature = null;
        diagnostic = string.Empty;

        try
        {
            var value = StripFieldNameIfPresent(DkimCanonicalizer.UnfoldHeaderValue(headerValue));
            var tags = ParseTags(value);

            var version = GetRequiredTag(tags, "v");
            if (!version.Equals("1", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("The DKIM-Signature v tag is not supported.");
            }

            var algorithm = GetRequiredTag(tags, "a").ToLowerInvariant();
            if (algorithm is not "rsa-sha1" and not "rsa-sha256")
            {
                throw new FormatException("The DKIM-Signature a tag is not supported.");
            }

            var domain = NormalizeRequiredTag(tags, "d");
            var selector = NormalizeRequiredTag(tags, "s");
            var bodyHash = RemoveWhitespace(GetRequiredTag(tags, "bh"));
            var signatureValue = RemoveWhitespace(GetRequiredTag(tags, "b"));
            var signedHeaders = ParseSignedHeaders(GetRequiredTag(tags, "h"));
            if (!signedHeaders.Any(static header => header.Equals("from", StringComparison.OrdinalIgnoreCase)))
            {
                throw new FormatException("The DKIM-Signature h tag must include the From header.");
            }

            ParseCanonicalization(
                tags.TryGetValue("c", out var canonicalization) ? canonicalization : string.Empty,
                out var headerCanonicalization,
                out var bodyCanonicalization);

            var queryMethod = tags.TryGetValue("q", out var queryValue) && queryValue.Length > 0
                ? queryValue.ToLowerInvariant()
                : "dns/txt";
            if (!queryMethod.Equals("dns/txt", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("The DKIM-Signature q tag is not supported.");
            }

            string? identity = null;
            if (tags.TryGetValue("i", out var identityValue) && identityValue.Length > 0)
            {
                identity = DecodeDkimQuotedPrintable(identityValue);
                ValidateIdentityDomain(identity, domain);
            }

            int? bodyLength = null;
            if (tags.TryGetValue("l", out var lengthValue) && lengthValue.Length > 0)
            {
                if (!int.TryParse(lengthValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedLength)
                    || parsedLength < 0)
                {
                    throw new FormatException("The DKIM-Signature l tag is not a non-negative decimal integer.");
                }

                bodyLength = parsedLength;
            }

            signature = new DkimSignature(
                version,
                algorithm,
                domain,
                selector,
                signedHeaders,
                bodyHash,
                signatureValue,
                headerCanonicalization,
                bodyCanonicalization,
                queryMethod,
                identity,
                bodyLength,
                tags);
            return true;
        }
        catch (FormatException exception)
        {
            diagnostic = exception.Message;
            return false;
        }
    }

    private static Dictionary<string, string> ParseTags(string value)
    {
        var tags = new Dictionary<string, string>(TagComparer);
        foreach (var rawPart in value.Split(';'))
        {
            var part = rawPart.Trim();
            if (part.Length == 0)
            {
                continue;
            }

            var equalsIndex = part.IndexOf('=');
            if (equalsIndex <= 0)
            {
                throw new FormatException("A DKIM-Signature tag is missing its value separator.");
            }

            var tagName = part[..equalsIndex].Trim();
            var tagValue = part[(equalsIndex + 1)..].Trim();
            ValidateTagName(tagName);

            if (!tags.TryAdd(tagName, NormalizeTagValue(tagName, tagValue)))
            {
                throw new FormatException($"The DKIM-Signature {tagName} tag appears more than once.");
            }
        }

        if (tags.Count == 0)
        {
            throw new FormatException("The DKIM-Signature header does not contain any tags.");
        }

        return tags;
    }

    private static string NormalizeTagValue(string tagName, string value)
    {
        if (tagName.Equals("h", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(
                ":",
                value.Split(':')
                    .Select(static header => header.Trim())
                    .Where(static header => header.Length > 0));
        }

        return tagName.Equals("b", StringComparison.OrdinalIgnoreCase)
               || tagName.Equals("bh", StringComparison.OrdinalIgnoreCase)
            ? RemoveWhitespace(value)
            : value.Trim();
    }

    private static string GetRequiredTag(IReadOnlyDictionary<string, string> tags, string tagName)
    {
        if (!tags.TryGetValue(tagName, out var value) || value.Length == 0)
        {
            throw new FormatException($"The DKIM-Signature {tagName} tag is required.");
        }

        return value;
    }

    private static string NormalizeRequiredTag(IReadOnlyDictionary<string, string> tags, string tagName)
    {
        var value = GetRequiredTag(tags, tagName).Trim().TrimEnd('.');
        if (value.Length == 0)
        {
            throw new FormatException($"The DKIM-Signature {tagName} tag is empty.");
        }

        return value.ToLowerInvariant();
    }

    private static IReadOnlyList<string> ParseSignedHeaders(string value)
    {
        var headers = value.Split(':')
            .Select(static header => header.Trim())
            .Where(static header => header.Length > 0)
            .ToArray();
        if (headers.Length == 0)
        {
            throw new FormatException("The DKIM-Signature h tag does not contain any header fields.");
        }

        return headers;
    }

    private static void ParseCanonicalization(
        string value,
        out DkimCanonicalizationMethod headerCanonicalization,
        out DkimCanonicalizationMethod bodyCanonicalization)
    {
        headerCanonicalization = DkimCanonicalizationMethod.Simple;
        bodyCanonicalization = DkimCanonicalizationMethod.Simple;
        if (value.Length == 0)
        {
            return;
        }

        var parts = value.Split('/');
        if (parts.Length is < 1 or > 2 || parts.Any(static part => part.Length == 0))
        {
            throw new FormatException("The DKIM-Signature c tag is malformed.");
        }

        headerCanonicalization = ParseCanonicalizationMethod(parts[0]);
        bodyCanonicalization = parts.Length == 1
            ? DkimCanonicalizationMethod.Simple
            : ParseCanonicalizationMethod(parts[1]);
    }

    private static DkimCanonicalizationMethod ParseCanonicalizationMethod(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "simple" => DkimCanonicalizationMethod.Simple,
            "relaxed" => DkimCanonicalizationMethod.Relaxed,
            _ => throw new FormatException("The DKIM-Signature c tag contains an unsupported canonicalization method.")
        };

    private static void ValidateIdentityDomain(string identity, string domain)
    {
        var atIndex = identity.LastIndexOf('@');
        if (atIndex < 0 || atIndex == identity.Length - 1)
        {
            return;
        }

        var identityDomain = identity[(atIndex + 1)..].TrimEnd('.');
        if (!identityDomain.Equals(domain, StringComparison.OrdinalIgnoreCase)
            && !identityDomain.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("The DKIM-Signature i tag domain is not the signing domain or a child domain.");
        }
    }

    private static string DecodeDkimQuotedPrintable(string value)
    {
        var compact = RemoveWhitespace(value);
        var builder = new StringBuilder(compact.Length);
        for (var index = 0; index < compact.Length; index++)
        {
            if (compact[index] != '=')
            {
                builder.Append(compact[index]);
                continue;
            }

            if (index + 2 >= compact.Length
                || !byte.TryParse(
                    compact.AsSpan(index + 1, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var decoded))
            {
                throw new FormatException("The DKIM-Signature i tag contains invalid quoted-printable data.");
            }

            builder.Append((char)decoded);
            index += 2;
        }

        return builder.ToString();
    }

    private static string StripFieldNameIfPresent(string value)
    {
        var colonIndex = value.IndexOf(':');
        if (colonIndex <= 0)
        {
            return value.Trim();
        }

        var name = value[..colonIndex].Trim();
        return name.Equals("DKIM-Signature", StringComparison.OrdinalIgnoreCase)
            ? value[(colonIndex + 1)..].Trim()
            : value.Trim();
    }

    private static void ValidateTagName(string value)
    {
        if (value.Length == 0
            || !char.IsAsciiLetter(value[0])
            || value.Any(static character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new FormatException("A DKIM-Signature tag name is malformed.");
        }
    }

    private static string RemoveWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!DkimCanonicalizer.IsWhitespace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
