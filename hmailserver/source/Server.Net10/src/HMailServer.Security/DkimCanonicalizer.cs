using System.Text;

namespace HMailServer.Security;

public static class DkimCanonicalizer
{
    public static string CanonicalizeBody(
        string body,
        DkimCanonicalizationMethod method)
    {
        ArgumentNullException.ThrowIfNull(body);

        return method switch
        {
            DkimCanonicalizationMethod.Simple => CanonicalizeSimpleBody(body),
            DkimCanonicalizationMethod.Relaxed => CanonicalizeRelaxedBody(body),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported DKIM body canonicalization method.")
        };
    }

    public static string CanonicalizeHeaderLine(
        string name,
        string value,
        DkimCanonicalizationMethod method)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        return method switch
        {
            DkimCanonicalizationMethod.Simple => name + ": " + value,
            DkimCanonicalizationMethod.Relaxed => CanonicalizeHeaderName(name) + ":" + CanonicalizeHeaderValue(value),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported DKIM header canonicalization method.")
        };
    }

    public static string CanonicalizeHeaders(
        string headerBlock,
        string signatureHeaderName,
        string signatureHeaderValue,
        IReadOnlyList<string> signedHeaderNames,
        DkimCanonicalizationMethod method,
        out string fieldList)
    {
        ArgumentNullException.ThrowIfNull(headerBlock);
        ArgumentNullException.ThrowIfNull(signatureHeaderName);
        ArgumentNullException.ThrowIfNull(signatureHeaderValue);
        ArgumentNullException.ThrowIfNull(signedHeaderNames);

        var fields = ParseHeaderFields(headerBlock);
        var result = new StringBuilder(headerBlock.Length + signatureHeaderValue.Length);
        var includedFields = new List<string>();

        foreach (var requestedName in signedHeaderNames)
        {
            var trimmedName = requestedName.Trim();
            if (trimmedName.Length == 0)
            {
                continue;
            }

            var fieldIndex = FindLastFieldIndex(fields, trimmedName);
            if (fieldIndex < 0)
            {
                continue;
            }

            var field = fields[fieldIndex];
            if (method == DkimCanonicalizationMethod.Simple)
            {
                result.Append(field.RawLine).Append("\r\n");
                includedFields.Add(field.Name);
            }
            else
            {
                result.Append(CanonicalizeHeaderLine(trimmedName, field.Value, method)).Append("\r\n");
                includedFields.Add(trimmedName);
            }

            fields.RemoveAt(fieldIndex);
        }

        fieldList = string.Join(":", includedFields);

        if (signatureHeaderName.Trim().Length > 0)
        {
            var signatureValueWithoutSignature = RemoveSignatureValue(signatureHeaderValue);
            var signatureLine = CanonicalizeHeaderLine(
                signatureHeaderName,
                signatureValueWithoutSignature,
                method);
            if (signatureLine.EndsWith("\r\n", StringComparison.Ordinal))
            {
                signatureLine = signatureLine[..^2];
            }

            result.Append(signatureLine);
        }

        return result.ToString();
    }

    public static string RemoveSignatureValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var position = value.IndexOf("b=", StringComparison.OrdinalIgnoreCase);
        if (position < 0)
        {
            return string.Empty;
        }

        var end = value.IndexOf(';', position);
        var actualEnd = end > position ? end : value.Length;
        return value[..(position + 2)] + value[actualEnd..];
    }

    internal static string UnfoldHeaderValue(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (index + 2 < value.Length
                && value[index] == '\r'
                && value[index + 1] == '\n'
                && IsWhitespace(value[index + 2]))
            {
                builder.Append(' ');
                index += 2;
                while (index + 1 < value.Length && IsWhitespace(value[index + 1]))
                {
                    index++;
                }

                continue;
            }

            builder.Append(value[index]);
        }

        return builder.ToString();
    }

    internal static bool IsWhitespace(char value) =>
        value is ' ' or '\t' or '\r' or '\n';

    private static string CanonicalizeSimpleBody(string body)
    {
        while (body.EndsWith("\r\n", StringComparison.Ordinal))
        {
            body = body[..^2];
        }

        return body + "\r\n";
    }

    private static string CanonicalizeRelaxedBody(string body)
    {
        var lines = body.Split("\r\n", StringSplitOptions.None)
            .Select(static line => CompressWhitespace(line).TrimEnd(' ', '\t'))
            .ToArray();
        var lastNonEmpty = Array.FindLastIndex(lines, static line => line.Length > 0);
        if (lastNonEmpty < 0)
        {
            return string.Empty;
        }

        return string.Join("\r\n", lines.Take(lastNonEmpty + 1)) + "\r\n";
    }

    private static string CanonicalizeHeaderName(string name) =>
        name.Trim().ToLowerInvariant();

    private static string CanonicalizeHeaderValue(string value) =>
        CompressWhitespace(UnfoldHeaderValue(value)).Trim(' ', '\t');

    private static string CompressWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var inWhitespace = false;
        foreach (var character in value)
        {
            if (character is ' ' or '\t')
            {
                if (!inWhitespace)
                {
                    builder.Append(' ');
                    inWhitespace = true;
                }

                continue;
            }

            builder.Append(character);
            inWhitespace = false;
        }

        return builder.ToString();
    }

    private static List<HeaderField> ParseHeaderFields(string headerBlock)
    {
        var fields = new List<HeaderField>();
        var current = new StringBuilder();

        foreach (var line in headerBlock.Split("\r\n", StringSplitOptions.None))
        {
            if (line.Length == 0)
            {
                continue;
            }

            if ((line[0] == ' ' || line[0] == '\t') && current.Length > 0)
            {
                current.Append("\r\n").Append(line);
                continue;
            }

            AddCurrentField(fields, current);
            current.Clear();
            current.Append(line);
        }

        AddCurrentField(fields, current);
        return fields;
    }

    private static void AddCurrentField(
        ICollection<HeaderField> fields,
        StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        var rawLine = current.ToString();
        var colonIndex = rawLine.IndexOf(':');
        if (colonIndex <= 0)
        {
            return;
        }

        fields.Add(new HeaderField(
            rawLine[..colonIndex],
            rawLine[(colonIndex + 1)..],
            rawLine));
    }

    private static int FindLastFieldIndex(
        IReadOnlyList<HeaderField> fields,
        string name)
    {
        for (var index = fields.Count - 1; index >= 0; index--)
        {
            if (fields[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private sealed record HeaderField(
        string Name,
        string Value,
        string RawLine);
}
