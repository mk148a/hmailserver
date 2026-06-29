namespace HMailServer.Security;

public enum DmarcResult
{
    None,
    Pass,
    Fail,
    TempError,
    PermError
}

public enum DmarcPolicy
{
    None,
    Quarantine,
    Reject
}

public enum DmarcAlignmentMode
{
    Relaxed,
    Strict
}

public enum DmarcTxtStatus
{
    Success,
    NoData,
    NameError,
    TemporaryError
}

public sealed record DmarcTxtResponse
{
    private DmarcTxtResponse(DmarcTxtStatus status, IReadOnlyList<string> records)
    {
        Status = status;
        Records = records;
    }

    public DmarcTxtStatus Status { get; }

    public IReadOnlyList<string> Records { get; }

    public static DmarcTxtResponse Success(params string[] records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return new DmarcTxtResponse(DmarcTxtStatus.Success, records.ToArray());
    }

    public static DmarcTxtResponse NoData() =>
        new(DmarcTxtStatus.NoData, Array.Empty<string>());

    public static DmarcTxtResponse NameError() =>
        new(DmarcTxtStatus.NameError, Array.Empty<string>());

    public static DmarcTxtResponse TemporaryError() =>
        new(DmarcTxtStatus.TemporaryError, Array.Empty<string>());
}

public interface IDmarcTxtResolver
{
    ValueTask<DmarcTxtResponse> QueryTxtAsync(
        string domain,
        CancellationToken cancellationToken);
}

public sealed class SystemDmarcTxtResolver : IDmarcTxtResolver
{
    private readonly ISpfDnsResolver _resolver;

    public SystemDmarcTxtResolver()
        : this(new SystemSpfDnsResolver())
    {
    }

    public SystemDmarcTxtResolver(ISpfDnsResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async ValueTask<DmarcTxtResponse> QueryTxtAsync(
        string domain,
        CancellationToken cancellationToken)
    {
        var response = await _resolver.QueryTxtAsync(domain, cancellationToken)
            .ConfigureAwait(false);
        return response.Status switch
        {
            SpfDnsStatus.Success => DmarcTxtResponse.Success(response.Records.ToArray()),
            SpfDnsStatus.NameError => DmarcTxtResponse.NameError(),
            SpfDnsStatus.TemporaryError => DmarcTxtResponse.TemporaryError(),
            _ => DmarcTxtResponse.NoData()
        };
    }
}

public sealed record DmarcSpfAuthenticationResult(
    bool Passed,
    string Domain);

public sealed record DmarcDkimAuthenticationResult(
    bool Passed,
    string Domain);

public sealed record DmarcEvaluationRequest(
    string HeaderFromDomain,
    DmarcSpfAuthenticationResult? Spf = null,
    IReadOnlyList<DmarcDkimAuthenticationResult>? Dkim = null,
    string? OrganizationalDomain = null);

public sealed record DmarcRecord(
    string Domain,
    string QueryName,
    DmarcPolicy Policy,
    DmarcPolicy? SubdomainPolicy,
    DmarcAlignmentMode SpfAlignment,
    DmarcAlignmentMode DkimAlignment,
    int Percentage,
    IReadOnlyDictionary<string, string> Tags);

public sealed record DmarcEvaluation(
    DmarcResult Result,
    DmarcPolicy AppliedPolicy,
    string Domain,
    string Diagnostic,
    DmarcRecord? Record);

public static class DmarcEvaluator
{
    private static readonly StringComparer TagComparer = StringComparer.OrdinalIgnoreCase;

    public static async ValueTask<DmarcEvaluation> EvaluateAsync(
        DmarcEvaluationRequest request,
        IDmarcTxtResolver resolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolver);

        if (!TryNormalizeDomain(request.HeaderFromDomain, out var headerFromDomain))
        {
            return Failure(
                DmarcResult.PermError,
                DmarcPolicy.None,
                string.Empty,
                "DMARC evaluation failed: the RFC5322.From domain is malformed.");
        }

        string? organizationalDomain = null;
        if (!string.IsNullOrWhiteSpace(request.OrganizationalDomain))
        {
            if (!TryNormalizeDomain(request.OrganizationalDomain, out organizationalDomain)
                || !IsSameOrSubdomain(headerFromDomain, organizationalDomain))
            {
                return Failure(
                    DmarcResult.PermError,
                    DmarcPolicy.None,
                    headerFromDomain,
                    "DMARC evaluation failed: the organizational domain is malformed or not a parent of RFC5322.From.");
            }
        }

        var lookup = await LookupRecordAsync(
                headerFromDomain,
                organizationalDomain,
                resolver,
                cancellationToken)
            .ConfigureAwait(false);
        if (lookup.Evaluation is not null)
        {
            return lookup.Evaluation with { Domain = headerFromDomain };
        }

        var record = lookup.Record;
        if (record is null)
        {
            return new DmarcEvaluation(
                DmarcResult.None,
                DmarcPolicy.None,
                headerFromDomain,
                "No DMARC policy record was found.",
                null);
        }

        var policy = GetEffectivePolicy(record, headerFromDomain);
        var spfAligned = request.Spf is { Passed: true }
                         && IsAligned(
                             request.Spf.Domain,
                             headerFromDomain,
                             record.SpfAlignment,
                             organizationalDomain);
        var dkimAligned = (request.Dkim ?? Array.Empty<DmarcDkimAuthenticationResult>())
            .Any(result => result.Passed
                           && IsAligned(
                               result.Domain,
                               headerFromDomain,
                               record.DkimAlignment,
                               organizationalDomain));

        if (spfAligned || dkimAligned)
        {
            return new DmarcEvaluation(
                DmarcResult.Pass,
                DmarcPolicy.None,
                headerFromDomain,
                spfAligned
                    ? "DMARC passed with an aligned SPF pass."
                    : "DMARC passed with an aligned DKIM pass.",
                record);
        }

        return new DmarcEvaluation(
            DmarcResult.Fail,
            policy,
            headerFromDomain,
            "DMARC failed: neither SPF nor DKIM produced an aligned pass.",
            record);
    }

    private static async ValueTask<DmarcRecordLookup> LookupRecordAsync(
        string headerFromDomain,
        string? organizationalDomain,
        IDmarcTxtResolver resolver,
        CancellationToken cancellationToken)
    {
        var exact = await QueryAndParseAsync(headerFromDomain, resolver, cancellationToken)
            .ConfigureAwait(false);
        if (exact.ShouldStop)
        {
            return exact;
        }

        if (organizationalDomain is null
            || organizationalDomain.Equals(headerFromDomain, StringComparison.OrdinalIgnoreCase))
        {
            return exact;
        }

        var organizational = await QueryAndParseAsync(
                organizationalDomain,
                resolver,
                cancellationToken)
            .ConfigureAwait(false);
        return organizational.ShouldStop ? organizational : exact;
    }

    private static async ValueTask<DmarcRecordLookup> QueryAndParseAsync(
        string domain,
        IDmarcTxtResolver resolver,
        CancellationToken cancellationToken)
    {
        var queryName = "_dmarc." + domain;
        var response = await resolver.QueryTxtAsync(queryName, cancellationToken)
            .ConfigureAwait(false);
        return response.Status switch
        {
            DmarcTxtStatus.TemporaryError => new DmarcRecordLookup(
                null,
                Failure(
                    DmarcResult.TempError,
                    DmarcPolicy.None,
                    domain,
                    "DMARC record lookup failed temporarily."),
                ShouldStop: true),
            DmarcTxtStatus.NoData or DmarcTxtStatus.NameError => DmarcRecordLookup.NotFound(),
            _ => ValidateRecords(domain, queryName, response.Records)
        };
    }

    private static DmarcRecordLookup ValidateRecords(
        string domain,
        string queryName,
        IReadOnlyList<string> records)
    {
        var dmarcRecords = records
            .Where(static record => record.TrimStart().StartsWith(
                "v=DMARC1",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (dmarcRecords.Length == 0)
        {
            return DmarcRecordLookup.NotFound();
        }

        if (dmarcRecords.Length > 1)
        {
            return new DmarcRecordLookup(
                null,
                Failure(
                    DmarcResult.PermError,
                    DmarcPolicy.None,
                    domain,
                    "DMARC record lookup failed: multiple DMARC records were found."),
                ShouldStop: true);
        }

        try
        {
            return new DmarcRecordLookup(
                ParseRecord(domain, queryName, dmarcRecords[0]),
                null,
                ShouldStop: true);
        }
        catch (FormatException exception)
        {
            return new DmarcRecordLookup(
                null,
                Failure(
                    DmarcResult.PermError,
                    DmarcPolicy.None,
                    domain,
                    "DMARC record lookup failed: " + exception.Message),
                ShouldStop: true);
        }
    }

    private static DmarcRecord ParseRecord(string domain, string queryName, string value)
    {
        ValidateRecordCharacters(value);
        var tags = ParseTags(value);
        if (!tags.TryGetValue("v", out var version)
            || !version.Equals("DMARC1", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("the v tag is missing or is not DMARC1.");
        }

        if (!tags.TryGetValue("p", out var policyValue))
        {
            throw new FormatException("the required p tag is missing.");
        }

        var policy = ParsePolicy(policyValue, "p");
        var subdomainPolicy = tags.TryGetValue("sp", out var sp)
            ? ParsePolicy(sp, "sp")
            : (DmarcPolicy?)null;
        var spfAlignment = tags.TryGetValue("aspf", out var aspf)
            ? ParseAlignment(aspf, "aspf")
            : DmarcAlignmentMode.Relaxed;
        var dkimAlignment = tags.TryGetValue("adkim", out var adkim)
            ? ParseAlignment(adkim, "adkim")
            : DmarcAlignmentMode.Relaxed;
        var percentage = tags.TryGetValue("pct", out var pct)
            ? ParsePercentage(pct)
            : 100;

        return new DmarcRecord(
            domain,
            queryName,
            policy,
            subdomainPolicy,
            spfAlignment,
            dkimAlignment,
            percentage,
            tags);
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
                throw new FormatException("a tag is missing its value separator.");
            }

            var tagName = part[..equalsIndex].Trim();
            ValidateTagName(tagName);
            if (!tags.TryAdd(tagName, part[(equalsIndex + 1)..].Trim()))
            {
                throw new FormatException($"the {tagName} tag appears more than once.");
            }
        }

        return tags;
    }

    private static DmarcPolicy ParsePolicy(string value, string tagName) =>
        value.ToLowerInvariant() switch
        {
            "none" => DmarcPolicy.None,
            "quarantine" => DmarcPolicy.Quarantine,
            "reject" => DmarcPolicy.Reject,
            _ => throw new FormatException($"the {tagName} tag has an unsupported policy value.")
        };

    private static DmarcAlignmentMode ParseAlignment(string value, string tagName) =>
        value.ToLowerInvariant() switch
        {
            "r" => DmarcAlignmentMode.Relaxed,
            "s" => DmarcAlignmentMode.Strict,
            _ => throw new FormatException($"the {tagName} tag must be r or s.")
        };

    private static int ParsePercentage(string value)
    {
        if (value.Length == 0
            || !value.All(char.IsAsciiDigit)
            || !int.TryParse(value, out var percentage)
            || percentage is < 0 or > 100)
        {
            throw new FormatException("the pct tag must be an integer between 0 and 100.");
        }

        return percentage;
    }

    private static DmarcPolicy GetEffectivePolicy(DmarcRecord record, string headerFromDomain) =>
        !record.Domain.Equals(headerFromDomain, StringComparison.OrdinalIgnoreCase)
        && record.SubdomainPolicy is not null
            ? record.SubdomainPolicy.Value
            : record.Policy;

    private static bool IsAligned(
        string authenticationDomain,
        string headerFromDomain,
        DmarcAlignmentMode alignmentMode,
        string? organizationalDomain)
    {
        if (!TryNormalizeDomain(authenticationDomain, out var normalizedAuthenticationDomain))
        {
            return false;
        }

        if (normalizedAuthenticationDomain.Equals(headerFromDomain, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (alignmentMode == DmarcAlignmentMode.Strict)
        {
            return false;
        }

        if (organizationalDomain is not null
            && IsSameOrSubdomain(normalizedAuthenticationDomain, organizationalDomain)
            && IsSameOrSubdomain(headerFromDomain, organizationalDomain))
        {
            return true;
        }

        return IsSameOrSubdomain(normalizedAuthenticationDomain, headerFromDomain)
               || IsSameOrSubdomain(headerFromDomain, normalizedAuthenticationDomain);
    }

    private static bool IsSameOrSubdomain(string domain, string parentDomain) =>
        domain.Equals(parentDomain, StringComparison.OrdinalIgnoreCase)
        || domain.EndsWith("." + parentDomain, StringComparison.OrdinalIgnoreCase);

    private static bool TryNormalizeDomain(string value, out string normalized)
    {
        normalized = value.Trim().TrimEnd('.').ToLowerInvariant();
        if (normalized.Length == 0 || normalized.Length > 253)
        {
            return false;
        }

        var labels = normalized.Split('.');
        return labels.Length > 1
               && labels.All(static label =>
                   label.Length is > 0 and <= 63
                   && label[0] != '-'
                   && label[^1] != '-'
                   && label.All(static character =>
                       char.IsAsciiLetterOrDigit(character) || character == '-'));
    }

    private static void ValidateRecordCharacters(string value)
    {
        if (value.Any(static character => character is < (char)0x20 or > (char)0x7E))
        {
            throw new FormatException("the record contains non-ASCII or control characters.");
        }
    }

    private static void ValidateTagName(string value)
    {
        if (value.Length == 0
            || !char.IsAsciiLetter(value[0])
            || value.Skip(1).Any(static character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new FormatException("a tag name is malformed.");
        }
    }

    private static DmarcEvaluation Failure(
        DmarcResult result,
        DmarcPolicy appliedPolicy,
        string domain,
        string diagnostic) =>
        new(result, appliedPolicy, domain, diagnostic, null);

    private sealed record DmarcRecordLookup(
        DmarcRecord? Record,
        DmarcEvaluation? Evaluation,
        bool ShouldStop)
    {
        public static DmarcRecordLookup NotFound() =>
            new(null, null, ShouldStop: false);
    }
}
