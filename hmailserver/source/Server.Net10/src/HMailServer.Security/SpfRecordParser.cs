using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace HMailServer.Security;

internal enum SpfQualifier
{
    Pass,
    Fail,
    SoftFail,
    Neutral
}

internal enum SpfMechanismKind
{
    All,
    Include,
    Address,
    Mx,
    Ptr,
    Ip4,
    Ip6,
    Exists
}

internal sealed record SpfDirective(
    SpfQualifier Qualifier,
    SpfMechanismKind Kind,
    string Raw,
    string? DomainSpec = null,
    IPAddress? Network = null,
    int Ipv4PrefixLength = 32,
    int Ipv6PrefixLength = 128);

internal sealed record SpfRecord(
    IReadOnlyList<SpfDirective> Directives,
    string? Redirect,
    string? Explanation,
    bool HasAll);

internal static class SpfRecordParser
{
    private const string Version = "v=spf1";
    private const string MacroDelimiters = ".-+,/_=";

    public static bool TryParse(string value, out SpfRecord? record, out string diagnostic)
    {
        record = null;
        diagnostic = string.Empty;

        try
        {
            ValidateRecordCharacters(value);
            if (!value.StartsWith(Version, StringComparison.OrdinalIgnoreCase)
                || (value.Length > Version.Length && value[Version.Length] != ' '))
            {
                throw new FormatException("The record does not start with an exact v=spf1 version section.");
            }

            var directives = new List<SpfDirective>();
            string? redirect = null;
            string? explanation = null;
            var terms = value[Version.Length..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var term in terms)
            {
                ParseTerm(term, directives, ref redirect, ref explanation);
            }

            record = new SpfRecord(
                directives,
                redirect,
                explanation,
                directives.Any(static directive => directive.Kind == SpfMechanismKind.All));
            return true;
        }
        catch (FormatException exception)
        {
            diagnostic = exception.Message;
            return false;
        }
    }

    private static void ParseTerm(
        string term,
        ICollection<SpfDirective> directives,
        ref string? redirect,
        ref string? explanation)
    {
        if (term.Length == 0)
        {
            throw new FormatException("An SPF term is empty.");
        }

        var body = term;
        var qualifier = SpfQualifier.Pass;
        var hasQualifier = TryReadQualifier(body[0], out qualifier);
        if (hasQualifier)
        {
            body = body[1..];
            if (body.Length == 0)
            {
                throw new FormatException("An SPF qualifier is missing its mechanism.");
            }
        }

        var equalsIndex = body.IndexOf('=');
        var mechanismDelimiter = FirstIndex(body, ':', '/');
        if (equalsIndex >= 0 && (mechanismDelimiter < 0 || equalsIndex < mechanismDelimiter))
        {
            if (hasQualifier)
            {
                throw new FormatException("SPF modifiers cannot have qualifiers.");
            }

            ParseModifier(body, equalsIndex, ref redirect, ref explanation);
            return;
        }

        directives.Add(ParseDirective(term, body, qualifier));
    }

    private static void ParseModifier(
        string body,
        int equalsIndex,
        ref string? redirect,
        ref string? explanation)
    {
        var name = body[..equalsIndex];
        var macroString = body[(equalsIndex + 1)..];
        ValidateName(name);
        ValidateMacroString(macroString, allowExplanationOnlyMacros: false);

        if (name.Equals("redirect", StringComparison.OrdinalIgnoreCase))
        {
            if (redirect is not null || macroString.Length == 0)
            {
                throw new FormatException("The redirect modifier is empty or appears more than once.");
            }

            redirect = macroString;
        }
        else if (name.Equals("exp", StringComparison.OrdinalIgnoreCase))
        {
            if (explanation is not null || macroString.Length == 0)
            {
                throw new FormatException("The exp modifier is empty or appears more than once.");
            }

            explanation = macroString;
        }
    }

    private static SpfDirective ParseDirective(
        string raw,
        string body,
        SpfQualifier qualifier)
    {
        var delimiterIndex = FirstIndex(body, ':', '/');
        var name = delimiterIndex < 0 ? body : body[..delimiterIndex];
        var tail = delimiterIndex < 0 ? string.Empty : body[delimiterIndex..];

        return name.ToLowerInvariant() switch
        {
            "all" => tail.Length == 0
                ? new SpfDirective(qualifier, SpfMechanismKind.All, raw)
                : throw new FormatException("The all mechanism cannot have arguments."),
            "include" => ParseDomainMechanism(
                raw,
                qualifier,
                SpfMechanismKind.Include,
                tail,
                argumentRequired: true),
            "a" => ParseAddressMechanism(raw, qualifier, SpfMechanismKind.Address, tail),
            "mx" => ParseAddressMechanism(raw, qualifier, SpfMechanismKind.Mx, tail),
            "ptr" => ParseDomainMechanism(
                raw,
                qualifier,
                SpfMechanismKind.Ptr,
                tail,
                argumentRequired: false),
            "ip4" => ParseNetworkMechanism(raw, qualifier, SpfMechanismKind.Ip4, tail),
            "ip6" => ParseNetworkMechanism(raw, qualifier, SpfMechanismKind.Ip6, tail),
            "exists" => ParseDomainMechanism(
                raw,
                qualifier,
                SpfMechanismKind.Exists,
                tail,
                argumentRequired: true),
            _ => throw new FormatException($"Unknown SPF mechanism '{name}'.")
        };
    }

    private static SpfDirective ParseDomainMechanism(
        string raw,
        SpfQualifier qualifier,
        SpfMechanismKind kind,
        string tail,
        bool argumentRequired)
    {
        if (tail.Length == 0 && !argumentRequired)
        {
            return new SpfDirective(qualifier, kind, raw);
        }

        if (!tail.StartsWith(':') || tail.Length == 1)
        {
            throw new FormatException($"The {kind} mechanism has an invalid domain specification.");
        }

        var domainSpec = tail[1..];
        ValidateMacroString(domainSpec, allowExplanationOnlyMacros: false);
        return new SpfDirective(qualifier, kind, raw, DomainSpec: domainSpec);
    }

    private static SpfDirective ParseAddressMechanism(
        string raw,
        SpfQualifier qualifier,
        SpfMechanismKind kind,
        string tail)
    {
        string? domainSpec = null;
        var cidr = string.Empty;

        if (tail.StartsWith(':'))
        {
            var slashIndex = FindOutsideMacro(tail, '/', startIndex: 1);
            domainSpec = slashIndex < 0 ? tail[1..] : tail[1..slashIndex];
            cidr = slashIndex < 0 ? string.Empty : tail[slashIndex..];
            if (domainSpec.Length == 0)
            {
                throw new FormatException($"The {kind} mechanism has an empty domain specification.");
            }

            ValidateMacroString(domainSpec, allowExplanationOnlyMacros: false);
        }
        else if (tail.StartsWith('/'))
        {
            cidr = tail;
        }
        else if (tail.Length > 0)
        {
            throw new FormatException($"The {kind} mechanism has invalid syntax.");
        }

        ParseDualCidr(cidr, out var ipv4Prefix, out var ipv6Prefix);
        return new SpfDirective(
            qualifier,
            kind,
            raw,
            DomainSpec: domainSpec,
            Ipv4PrefixLength: ipv4Prefix,
            Ipv6PrefixLength: ipv6Prefix);
    }

    private static SpfDirective ParseNetworkMechanism(
        string raw,
        SpfQualifier qualifier,
        SpfMechanismKind kind,
        string tail)
    {
        if (!tail.StartsWith(':') || tail.Length == 1)
        {
            throw new FormatException($"The {kind} mechanism is missing its network.");
        }

        var networkText = tail[1..];
        var slashIndex = networkText.LastIndexOf('/');
        var addressText = slashIndex < 0 ? networkText : networkText[..slashIndex];
        var maximumPrefix = kind == SpfMechanismKind.Ip4 ? 32 : 128;
        var prefix = slashIndex < 0
            ? maximumPrefix
            : ParsePrefix(networkText[(slashIndex + 1)..], maximumPrefix);

        if (slashIndex >= 0 && networkText[..slashIndex].Contains('/'))
        {
            throw new FormatException($"The {kind} mechanism has more than one prefix length.");
        }

        IPAddress network;
        if (kind == SpfMechanismKind.Ip4)
        {
            if (!TryParseExactIpv4(addressText, out network))
            {
                throw new FormatException("The ip4 mechanism contains an invalid IPv4 network.");
            }
        }
        else if (!IPAddress.TryParse(addressText, out network!)
                 || network.AddressFamily != AddressFamily.InterNetworkV6
                 || network.ScopeId != 0)
        {
            throw new FormatException("The ip6 mechanism contains an invalid IPv6 network.");
        }

        return new SpfDirective(
            qualifier,
            kind,
            raw,
            Network: network,
            Ipv4PrefixLength: kind == SpfMechanismKind.Ip4 ? prefix : 32,
            Ipv6PrefixLength: kind == SpfMechanismKind.Ip6 ? prefix : 128);
    }

    private static void ParseDualCidr(
        string value,
        out int ipv4Prefix,
        out int ipv6Prefix)
    {
        ipv4Prefix = 32;
        ipv6Prefix = 128;
        if (value.Length == 0)
        {
            return;
        }

        if (!value.StartsWith('/'))
        {
            throw new FormatException("An SPF CIDR value must start with '/'.");
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            ipv6Prefix = ParsePrefix(value[2..], 128);
            return;
        }

        var ipv6Separator = value.IndexOf("//", 1, StringComparison.Ordinal);
        var ipv4Value = ipv6Separator < 0
            ? value[1..]
            : value[1..ipv6Separator];
        if (ipv4Value.Length == 0 || ipv4Value.Contains('/'))
        {
            throw new FormatException("An SPF dual CIDR value is malformed.");
        }

        ipv4Prefix = ParsePrefix(ipv4Value, 32);
        if (ipv6Separator >= 0)
        {
            var ipv6Value = value[(ipv6Separator + 2)..];
            if (ipv6Value.Contains('/'))
            {
                throw new FormatException("An SPF dual CIDR value is malformed.");
            }

            ipv6Prefix = ParsePrefix(ipv6Value, 128);
        }
    }

    private static int ParsePrefix(string value, int maximum)
    {
        if (value.Length == 0
            || !value.All(char.IsAsciiDigit)
            || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var prefix)
            || prefix < 0
            || prefix > maximum)
        {
            throw new FormatException($"An SPF prefix length must be between 0 and {maximum}.");
        }

        return prefix;
    }

    internal static bool ValidateMacroString(
        string value,
        bool allowExplanationOnlyMacros,
        out string diagnostic)
    {
        try
        {
            ValidateMacroString(value, allowExplanationOnlyMacros);
            diagnostic = string.Empty;
            return true;
        }
        catch (FormatException exception)
        {
            diagnostic = exception.Message;
            return false;
        }
    }

    private static void ValidateMacroString(string value, bool allowExplanationOnlyMacros)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (++index >= value.Length)
            {
                throw new FormatException("An SPF macro ends with an incomplete percent escape.");
            }

            if (value[index] is '%' or '_' or '-')
            {
                continue;
            }

            if (value[index] != '{')
            {
                throw new FormatException("An SPF macro contains an invalid percent escape.");
            }

            var closingBrace = value.IndexOf('}', index + 1);
            if (closingBrace < 0)
            {
                throw new FormatException("An SPF macro is missing its closing brace.");
            }

            ValidateMacroExpression(
                value[(index + 1)..closingBrace],
                allowExplanationOnlyMacros);
            index = closingBrace;
        }
    }

    private static void ValidateMacroExpression(
        string expression,
        bool allowExplanationOnlyMacros)
    {
        if (expression.Length == 0)
        {
            throw new FormatException("An SPF macro expression is empty.");
        }

        var letter = char.ToLowerInvariant(expression[0]);
        var valid = letter is 's' or 'l' or 'o' or 'd' or 'i' or 'p' or 'v' or 'h'
            || (allowExplanationOnlyMacros && letter is 'c' or 'r' or 't');
        if (!valid)
        {
            throw new FormatException($"SPF macro letter '{expression[0]}' is not valid here.");
        }

        var index = 1;
        var digitsStart = index;
        while (index < expression.Length && char.IsAsciiDigit(expression[index]))
        {
            index++;
        }

        if (index > digitsStart)
        {
            var digits = expression[digitsStart..index];
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                || count == 0)
            {
                throw new FormatException("An SPF macro transformer count must be nonzero.");
            }
        }

        if (index < expression.Length && char.ToLowerInvariant(expression[index]) == 'r')
        {
            index++;
        }

        for (; index < expression.Length; index++)
        {
            if (!MacroDelimiters.Contains(expression[index], StringComparison.Ordinal))
            {
                throw new FormatException("An SPF macro contains an invalid delimiter.");
            }
        }
    }

    private static bool TryReadQualifier(char value, out SpfQualifier qualifier)
    {
        qualifier = value switch
        {
            '-' => SpfQualifier.Fail,
            '~' => SpfQualifier.SoftFail,
            '?' => SpfQualifier.Neutral,
            _ => SpfQualifier.Pass
        };
        return value is '+' or '-' or '~' or '?';
    }

    private static void ValidateRecordCharacters(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Any(static character => character is < (char)0x20 or > (char)0x7E))
        {
            throw new FormatException("An SPF record contains non-ASCII or control characters.");
        }
    }

    private static void ValidateName(string value)
    {
        if (value.Length == 0
            || !char.IsAsciiLetter(value[0])
            || value.Skip(1).Any(
                static character =>
                    !char.IsAsciiLetterOrDigit(character)
                    && character is not '-' and not '_' and not '.'))
        {
            throw new FormatException("An SPF modifier name is malformed.");
        }
    }

    private static bool TryParseExactIpv4(string value, out IPAddress address)
    {
        address = IPAddress.None;
        var parts = value.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        var bytes = new byte[4];
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part.Length == 0
                || (part.Length > 1 && part[0] == '0')
                || !byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out bytes[index]))
            {
                return false;
            }
        }

        address = new IPAddress(bytes);
        return true;
    }

    private static int FirstIndex(string value, char first, char second)
    {
        var firstIndex = value.IndexOf(first);
        var secondIndex = value.IndexOf(second);
        if (firstIndex < 0)
        {
            return secondIndex;
        }

        return secondIndex < 0 ? firstIndex : Math.Min(firstIndex, secondIndex);
    }

    private static int FindOutsideMacro(string value, char character, int startIndex)
    {
        var inMacro = false;
        for (var index = startIndex; index < value.Length; index++)
        {
            if (value[index] == '%' && index + 1 < value.Length && value[index + 1] == '{')
            {
                inMacro = true;
                index++;
                continue;
            }

            if (inMacro && value[index] == '}')
            {
                inMacro = false;
                continue;
            }

            if (!inMacro && value[index] == character)
            {
                return index;
            }
        }

        return -1;
    }
}
