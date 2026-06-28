using System.Text;
using System.Text.RegularExpressions;

namespace HMailServer.Security;

public enum DkimTxtStatus
{
    Success,
    NoData,
    NameError,
    TemporaryError
}

public sealed record DkimTxtResponse
{
    private DkimTxtResponse(DkimTxtStatus status, IReadOnlyList<string> records)
    {
        Status = status;
        Records = records;
    }

    public DkimTxtStatus Status { get; }

    public IReadOnlyList<string> Records { get; }

    public static DkimTxtResponse Success(params string[] records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return new DkimTxtResponse(DkimTxtStatus.Success, records.ToArray());
    }

    public static DkimTxtResponse NoData() =>
        new(DkimTxtStatus.NoData, Array.Empty<string>());

    public static DkimTxtResponse NameError() =>
        new(DkimTxtStatus.NameError, Array.Empty<string>());

    public static DkimTxtResponse TemporaryError() =>
        new(DkimTxtStatus.TemporaryError, Array.Empty<string>());
}

public interface IDkimTxtResolver
{
    ValueTask<DkimTxtResponse> QueryTxtAsync(
        string domain,
        CancellationToken cancellationToken);
}

public sealed class SystemDkimTxtResolver : IDkimTxtResolver
{
    private readonly ISpfDnsResolver _resolver;

    public SystemDkimTxtResolver()
        : this(new SystemSpfDnsResolver())
    {
    }

    public SystemDkimTxtResolver(ISpfDnsResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async ValueTask<DkimTxtResponse> QueryTxtAsync(
        string domain,
        CancellationToken cancellationToken)
    {
        var response = await _resolver.QueryTxtAsync(domain, cancellationToken)
            .ConfigureAwait(false);
        return response.Status switch
        {
            SpfDnsStatus.Success => DkimTxtResponse.Success(response.Records.ToArray()),
            SpfDnsStatus.NameError => DkimTxtResponse.NameError(),
            SpfDnsStatus.TemporaryError => DkimTxtResponse.TemporaryError(),
            _ => DkimTxtResponse.NoData()
        };
    }
}

public sealed record DkimPublicKeyRecord(
    string QueryName,
    string PublicKey,
    string Flags,
    IReadOnlyDictionary<string, string> Tags);

public sealed record DkimPublicKeyLookupResult(
    DkimEvaluation Evaluation,
    DkimPublicKeyRecord? KeyRecord);

public static class DkimPublicKeyLookup
{
    private static readonly StringComparer TagComparer = StringComparer.OrdinalIgnoreCase;

    public static async ValueTask<DkimPublicKeyLookupResult> LookupAsync(
        DkimSignature signature,
        IDkimTxtResolver resolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(resolver);

        var queryName = $"{signature.Selector}._domainkey.{signature.Domain}";
        var response = await resolver.QueryTxtAsync(queryName, cancellationToken)
            .ConfigureAwait(false);
        return response.Status switch
        {
            DkimTxtStatus.TemporaryError => Failure(
                DkimResult.TempFail,
                "DKIM public key lookup failed temporarily."),
            DkimTxtStatus.NoData or DkimTxtStatus.NameError => Failure(
                DkimResult.PermFail,
                "DKIM public key lookup failed: no key for signature."),
            _ => ValidateKeyRecords(queryName, response.Records, signature)
        };
    }

    private static DkimPublicKeyLookupResult ValidateKeyRecords(
        string queryName,
        IReadOnlyList<string> records,
        DkimSignature signature)
    {
        if (records.Count == 0)
        {
            return Failure(
                DkimResult.PermFail,
                "DKIM public key lookup failed: no key for signature.");
        }

        IReadOnlyDictionary<string, string> tags;
        try
        {
            tags = ParseTags(records[0]);
        }
        catch (FormatException exception)
        {
            return Failure(
                DkimResult.PermFail,
                "DKIM public key lookup failed: " + exception.Message);
        }

        if (tags.Count == 0)
        {
            return Failure(
                DkimResult.PermFail,
                "DKIM public key lookup failed: the key record does not contain any tags.");
        }

        if (tags.TryGetValue("v", out var version)
            && !version.Equals("DKIM1", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                DkimResult.PermFail,
                "DKIM public key lookup failed: the v tag is not DKIM1.");
        }

        if (!tags.TryGetValue("p", out var publicKey) || publicKey.Length == 0)
        {
            return Failure(
                DkimResult.PermFail,
                "DKIM public key lookup failed: the public key is missing or revoked.");
        }

        if (tags.TryGetValue("g", out var granularity)
            && !MatchesGranularity(granularity, signature.Identity))
        {
            return Failure(
                DkimResult.PermFail,
                "DKIM public key lookup failed: the g tag does not match the signing identity.");
        }

        if (tags.TryGetValue("h", out var hashAlgorithms)
            && !IsHashAlgorithmAllowed(hashAlgorithms, signature.Algorithm))
        {
            return Failure(
                DkimResult.PermFail,
                "DKIM public key lookup failed: the h tag does not allow the signature hash algorithm.");
        }

        var flags = tags.TryGetValue("t", out var value) ? value : string.Empty;
        if (HasFlag(flags, "s") && !IdentityUsesExactDomain(signature.Identity, signature.Domain))
        {
            return Failure(
                DkimResult.PermFail,
                "DKIM public key lookup failed: the t=s flag requires the signing identity domain to exactly match d.");
        }

        return new DkimPublicKeyLookupResult(
            new DkimEvaluation(
                DkimResult.Neutral,
                "DKIM public key resolved; message signature is not evaluated in this lookup step."),
            new DkimPublicKeyRecord(
                queryName,
                publicKey,
                flags,
                tags));
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
                throw new FormatException("a key-record tag is missing its value separator.");
            }

            var tagName = part[..equalsIndex].Trim();
            ValidateTagName(tagName);
            if (!tags.TryAdd(tagName, NormalizeTagValue(tagName, part[(equalsIndex + 1)..])))
            {
                throw new FormatException($"the {tagName} tag appears more than once.");
            }
        }

        return tags;
    }

    private static string NormalizeTagValue(string tagName, string value)
    {
        var trimmed = value.Trim();
        return tagName.Equals("p", StringComparison.OrdinalIgnoreCase)
               || tagName.Equals("h", StringComparison.OrdinalIgnoreCase)
               || tagName.Equals("t", StringComparison.OrdinalIgnoreCase)
            ? RemoveWhitespace(trimmed)
            : trimmed;
    }

    private static void ValidateTagName(string value)
    {
        if (value.Length == 0
            || !char.IsAsciiLetter(value[0])
            || value.Any(static character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new FormatException("a key-record tag name is malformed.");
        }
    }

    private static bool MatchesGranularity(string granularity, string? identity)
    {
        var localPart = ExtractIdentityLocalPart(identity);
        if (localPart.Length == 0)
        {
            return granularity == "*";
        }

        var pattern = "^" + Regex.Escape(granularity)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(localPart, pattern, RegexOptions.CultureInvariant);
    }

    private static bool IsHashAlgorithmAllowed(string hashAlgorithms, string signatureAlgorithm)
    {
        var dashIndex = signatureAlgorithm.IndexOf('-');
        var hashName = dashIndex >= 0
            ? signatureAlgorithm[(dashIndex + 1)..]
            : signatureAlgorithm;
        return hashAlgorithms.Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Any(algorithm => algorithm.Equals(hashName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasFlag(string flags, string expected) =>
        flags.Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Any(flag => flag.Equals(expected, StringComparison.OrdinalIgnoreCase));

    private static bool IdentityUsesExactDomain(string? identity, string domain)
    {
        if (string.IsNullOrEmpty(identity))
        {
            return true;
        }

        var atIndex = identity.LastIndexOf('@');
        if (atIndex < 0 || atIndex == identity.Length - 1)
        {
            return false;
        }

        return identity[(atIndex + 1)..]
            .TrimEnd('.')
            .Equals(domain, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractIdentityLocalPart(string? identity)
    {
        if (string.IsNullOrEmpty(identity))
        {
            return string.Empty;
        }

        var atIndex = identity.LastIndexOf('@');
        return atIndex < 0
            ? identity
            : identity[..atIndex];
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

    private static DkimPublicKeyLookupResult Failure(
        DkimResult result,
        string diagnostic) =>
        new(
            new DkimEvaluation(result, diagnostic),
            null);
}
