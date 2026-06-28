using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HMailServer.Security;

public sealed class SpfEvaluator
{
    private readonly ISpfDnsResolver _resolver;
    private readonly SpfEvaluatorOptions _options;

    public SpfEvaluator(ISpfDnsResolver resolver, SpfEvaluatorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        _resolver = resolver;
        _options = options ?? new SpfEvaluatorOptions();
        ValidateOptions(_options);
    }

    public async ValueTask<SpfEvaluation> EvaluateAsync(
        SpfEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ClientAddress);

        var clientAddress = request.ClientAddress.IsIPv4MappedToIPv6
            ? request.ClientAddress.MapToIPv4()
            : request.ClientAddress;
        var requestedDomain = NormalizeDomain(request.Domain);
        var sender = NormalizeSender(request.Sender, requestedDomain);
        var normalizedRequest = request with
        {
            ClientAddress = clientAddress,
            Domain = requestedDomain,
            Sender = sender,
            HeloDomain = NormalizeDomain(request.HeloDomain)
        };
        var context = new EvaluationContext(normalizedRequest, _options);

        if (!TryValidateDomain(requestedDomain, strictHostName: true, out var initialDomain))
        {
            return context.Build(
                SpfResult.None,
                requestedDomain,
                matchedMechanism: null,
                "The initial SPF domain is malformed or is not a multi-label domain.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.EvaluationTimeout);

        try
        {
            var outcome = await EvaluateDomainAsync(
                    context,
                    initialDomain,
                    depth: 0,
                    countTxtVoid: false,
                    timeout.Token)
                .ConfigureAwait(false);
            return context.Build(
                outcome.Result,
                initialDomain,
                outcome.MatchedMechanism,
                outcome.Diagnostic);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return context.Build(
                SpfResult.TempError,
                initialDomain,
                matchedMechanism: null,
                "SPF evaluation exceeded its time budget.");
        }
    }

    private async ValueTask<DomainOutcome> EvaluateDomainAsync(
        EvaluationContext context,
        string domain,
        int depth,
        bool countTxtVoid,
        CancellationToken cancellationToken)
    {
        if (depth > _options.MaxRecursionDepth)
        {
            return DomainOutcome.Error(
                SpfResult.PermError,
                "SPF recursion exceeded the configured depth limit.");
        }

        if (!TryValidateDomain(domain, strictHostName: false, out var normalizedDomain))
        {
            return DomainOutcome.Error(
                SpfResult.None,
                "The recursively evaluated SPF domain is malformed.");
        }

        if (!context.ActiveDomains.Add(normalizedDomain))
        {
            return DomainOutcome.Error(
                SpfResult.PermError,
                "SPF include/redirect recursion contains a domain cycle.");
        }

        try
        {
            var txt = await QuerySafeAsync(
                    token => _resolver.QueryTxtAsync(normalizedDomain, token),
                    cancellationToken)
                .ConfigureAwait(false);
            if (txt.Status == SpfDnsStatus.TemporaryError)
            {
                return DomainOutcome.Error(
                    SpfResult.TempError,
                    $"TXT lookup for '{normalizedDomain}' failed temporarily.");
            }

            if (txt.Status is SpfDnsStatus.NoData or SpfDnsStatus.NameError
                || txt.Records.Count == 0)
            {
                if (countTxtVoid && !context.TryRegisterVoidLookup(out var limitDiagnostic))
                {
                    return DomainOutcome.Error(SpfResult.PermError, limitDiagnostic);
                }

                return DomainOutcome.Error(
                    SpfResult.None,
                    $"No SPF policy was found for '{normalizedDomain}'.");
            }

            var policies = txt.Records
                .Where(IsSpfPolicy)
                .ToArray();
            if (policies.Length == 0)
            {
                return DomainOutcome.Error(
                    SpfResult.None,
                    $"No SPF policy was found for '{normalizedDomain}'.");
            }

            if (policies.Length > 1)
            {
                return DomainOutcome.Error(
                    SpfResult.PermError,
                    $"More than one SPF policy was found for '{normalizedDomain}'.");
            }

            if (!SpfRecordParser.TryParse(policies[0], out var parsedRecord, out var parseDiagnostic)
                || parsedRecord is null)
            {
                return DomainOutcome.Error(SpfResult.PermError, parseDiagnostic);
            }

            foreach (var directive in parsedRecord.Directives)
            {
                var mechanism = await EvaluateMechanismAsync(
                        context,
                        normalizedDomain,
                        directive,
                        depth,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (mechanism.ErrorResult is not null)
                {
                    return DomainOutcome.Error(mechanism.ErrorResult.Value, mechanism.Diagnostic);
                }

                if (mechanism.Matched)
                {
                    return new DomainOutcome(
                        QualifierToResult(directive.Qualifier),
                        directive.Raw,
                        string.Empty);
                }
            }

            if (!parsedRecord.HasAll && parsedRecord.Redirect is not null)
            {
                if (!context.TryConsumeDnsTerm(out var limitDiagnostic))
                {
                    return DomainOutcome.Error(SpfResult.PermError, limitDiagnostic);
                }

                var expansion = await ExpandDomainSpecAsync(
                        context,
                        parsedRecord.Redirect,
                        normalizedDomain,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (expansion.ErrorResult is not null)
                {
                    return DomainOutcome.Error(expansion.ErrorResult.Value, expansion.Diagnostic);
                }

                var redirected = await EvaluateDomainAsync(
                        context,
                        expansion.Domain!,
                        depth + 1,
                        countTxtVoid: true,
                        cancellationToken)
                    .ConfigureAwait(false);
                return redirected.Result == SpfResult.None
                    ? DomainOutcome.Error(
                        SpfResult.PermError,
                        "An SPF redirect target did not publish a usable SPF record.")
                    : redirected;
            }

            return new DomainOutcome(
                SpfResult.Neutral,
                MatchedMechanism: null,
                "No SPF mechanism matched; the implicit neutral default applies.");
        }
        finally
        {
            context.ActiveDomains.Remove(normalizedDomain);
        }
    }

    private async ValueTask<MechanismOutcome> EvaluateMechanismAsync(
        EvaluationContext context,
        string currentDomain,
        SpfDirective directive,
        int depth,
        CancellationToken cancellationToken)
    {
        switch (directive.Kind)
        {
            case SpfMechanismKind.All:
                return MechanismOutcome.Match;

            case SpfMechanismKind.Ip4:
                return MechanismOutcome.FromMatch(
                    directive.Network is not null
                    && context.Request.ClientAddress.AddressFamily == AddressFamily.InterNetwork
                    && MatchesPrefix(
                        context.Request.ClientAddress,
                        directive.Network,
                        directive.Ipv4PrefixLength));

            case SpfMechanismKind.Ip6:
                return MechanismOutcome.FromMatch(
                    directive.Network is not null
                    && context.Request.ClientAddress.AddressFamily == AddressFamily.InterNetworkV6
                    && MatchesPrefix(
                        context.Request.ClientAddress,
                        directive.Network,
                        directive.Ipv6PrefixLength));

            case SpfMechanismKind.Include:
                return await EvaluateIncludeAsync(
                        context,
                        currentDomain,
                        directive,
                        depth,
                        cancellationToken)
                    .ConfigureAwait(false);

            case SpfMechanismKind.Address:
                return await EvaluateAddressAsync(
                        context,
                        currentDomain,
                        directive,
                        cancellationToken)
                    .ConfigureAwait(false);

            case SpfMechanismKind.Mx:
                return await EvaluateMxAsync(
                        context,
                        currentDomain,
                        directive,
                        cancellationToken)
                    .ConfigureAwait(false);

            case SpfMechanismKind.Ptr:
                return await EvaluatePtrAsync(
                        context,
                        currentDomain,
                        directive,
                        cancellationToken)
                    .ConfigureAwait(false);

            case SpfMechanismKind.Exists:
                return await EvaluateExistsAsync(
                        context,
                        currentDomain,
                        directive,
                        cancellationToken)
                    .ConfigureAwait(false);

            default:
                return MechanismOutcome.Error(
                    SpfResult.PermError,
                    "The SPF mechanism is not supported.");
        }
    }

    private async ValueTask<MechanismOutcome> EvaluateIncludeAsync(
        EvaluationContext context,
        string currentDomain,
        SpfDirective directive,
        int depth,
        CancellationToken cancellationToken)
    {
        if (!context.TryConsumeDnsTerm(out var limitDiagnostic))
        {
            return MechanismOutcome.Error(SpfResult.PermError, limitDiagnostic);
        }

        var expansion = await ExpandDomainSpecAsync(
                context,
                directive.DomainSpec!,
                currentDomain,
                cancellationToken)
            .ConfigureAwait(false);
        if (expansion.ErrorResult is not null)
        {
            return MechanismOutcome.Error(expansion.ErrorResult.Value, expansion.Diagnostic);
        }

        var included = await EvaluateDomainAsync(
                context,
                expansion.Domain!,
                depth + 1,
                countTxtVoid: true,
                cancellationToken)
            .ConfigureAwait(false);
        return included.Result switch
        {
            SpfResult.Pass => MechanismOutcome.Match,
            SpfResult.Fail or SpfResult.SoftFail or SpfResult.Neutral => MechanismOutcome.NoMatch,
            SpfResult.TempError => MechanismOutcome.Error(SpfResult.TempError, included.Diagnostic),
            SpfResult.PermError => MechanismOutcome.Error(SpfResult.PermError, included.Diagnostic),
            SpfResult.None => MechanismOutcome.Error(
                SpfResult.PermError,
                "An SPF include target did not publish a usable SPF record."),
            _ => MechanismOutcome.NoMatch
        };
    }

    private async ValueTask<MechanismOutcome> EvaluateAddressAsync(
        EvaluationContext context,
        string currentDomain,
        SpfDirective directive,
        CancellationToken cancellationToken)
    {
        if (!context.TryConsumeDnsTerm(out var limitDiagnostic))
        {
            return MechanismOutcome.Error(SpfResult.PermError, limitDiagnostic);
        }

        var target = await ResolveTargetDomainAsync(
                context,
                directive.DomainSpec,
                currentDomain,
                cancellationToken)
            .ConfigureAwait(false);
        if (target.ErrorResult is not null)
        {
            return MechanismOutcome.Error(target.ErrorResult.Value, target.Diagnostic);
        }

        var family = context.Request.ClientAddress.AddressFamily;
        var response = await QuerySafeAsync(
                token => _resolver.QueryAddressesAsync(target.Domain!, family, token),
                cancellationToken)
            .ConfigureAwait(false);
        var responseError = RegisterDnsResponse(context, response, target.Domain!);
        if (responseError is not null)
        {
            return responseError;
        }

        var prefix = family == AddressFamily.InterNetwork
            ? directive.Ipv4PrefixLength
            : directive.Ipv6PrefixLength;
        return MechanismOutcome.FromMatch(
            response.Records.Any(
                address =>
                    address.AddressFamily == family
                    && MatchesPrefix(context.Request.ClientAddress, address, prefix)));
    }

    private async ValueTask<MechanismOutcome> EvaluateMxAsync(
        EvaluationContext context,
        string currentDomain,
        SpfDirective directive,
        CancellationToken cancellationToken)
    {
        if (!context.TryConsumeDnsTerm(out var limitDiagnostic))
        {
            return MechanismOutcome.Error(SpfResult.PermError, limitDiagnostic);
        }

        var target = await ResolveTargetDomainAsync(
                context,
                directive.DomainSpec,
                currentDomain,
                cancellationToken)
            .ConfigureAwait(false);
        if (target.ErrorResult is not null)
        {
            return MechanismOutcome.Error(target.ErrorResult.Value, target.Diagnostic);
        }

        var mx = await QuerySafeAsync(
                token => _resolver.QueryMxAsync(target.Domain!, token),
                cancellationToken)
            .ConfigureAwait(false);
        var mxError = RegisterDnsResponse(context, mx, target.Domain!);
        if (mxError is not null)
        {
            return mxError;
        }

        if (mx.Records.Count > _options.MaxMxHosts)
        {
            return MechanismOutcome.Error(
                SpfResult.PermError,
                "The SPF mx mechanism exceeded its MX host limit.");
        }

        var family = context.Request.ClientAddress.AddressFamily;
        var prefix = family == AddressFamily.InterNetwork
            ? directive.Ipv4PrefixLength
            : directive.Ipv6PrefixLength;
        foreach (var host in mx.Records)
        {
            if (!TryValidateDomain(host.Exchange, strictHostName: false, out var exchange))
            {
                continue;
            }

            var addresses = await QuerySafeAsync(
                    token => _resolver.QueryAddressesAsync(exchange, family, token),
                    cancellationToken)
                .ConfigureAwait(false);
            var addressError = RegisterDnsResponse(context, addresses, exchange);
            if (addressError is not null)
            {
                return addressError;
            }

            if (addresses.Records.Any(
                    address =>
                        address.AddressFamily == family
                        && MatchesPrefix(context.Request.ClientAddress, address, prefix)))
            {
                return MechanismOutcome.Match;
            }
        }

        return MechanismOutcome.NoMatch;
    }

    private async ValueTask<MechanismOutcome> EvaluatePtrAsync(
        EvaluationContext context,
        string currentDomain,
        SpfDirective directive,
        CancellationToken cancellationToken)
    {
        if (!context.TryConsumeDnsTerm(out var limitDiagnostic))
        {
            return MechanismOutcome.Error(SpfResult.PermError, limitDiagnostic);
        }

        var target = await ResolveTargetDomainAsync(
                context,
                directive.DomainSpec,
                currentDomain,
                cancellationToken)
            .ConfigureAwait(false);
        if (target.ErrorResult is not null)
        {
            return MechanismOutcome.Error(target.ErrorResult.Value, target.Diagnostic);
        }

        var validated = await GetValidatedDomainsAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (validated.ErrorResult is not null)
        {
            return MechanismOutcome.Error(validated.ErrorResult.Value, validated.Diagnostic);
        }

        return MechanismOutcome.FromMatch(
            validated.Domains.Any(name => IsSameOrSubdomain(name, target.Domain!)));
    }

    private async ValueTask<MechanismOutcome> EvaluateExistsAsync(
        EvaluationContext context,
        string currentDomain,
        SpfDirective directive,
        CancellationToken cancellationToken)
    {
        if (!context.TryConsumeDnsTerm(out var limitDiagnostic))
        {
            return MechanismOutcome.Error(SpfResult.PermError, limitDiagnostic);
        }

        var target = await ResolveTargetDomainAsync(
                context,
                directive.DomainSpec,
                currentDomain,
                cancellationToken)
            .ConfigureAwait(false);
        if (target.ErrorResult is not null)
        {
            return MechanismOutcome.Error(target.ErrorResult.Value, target.Diagnostic);
        }

        var response = await QuerySafeAsync(
                token => _resolver.QueryAddressesAsync(
                    target.Domain!,
                    AddressFamily.InterNetwork,
                    token),
                cancellationToken)
            .ConfigureAwait(false);
        var responseError = RegisterDnsResponse(context, response, target.Domain!);
        return responseError ?? MechanismOutcome.FromMatch(response.Records.Count > 0);
    }

    private async ValueTask<ExpansionOutcome> ResolveTargetDomainAsync(
        EvaluationContext context,
        string? domainSpec,
        string currentDomain,
        CancellationToken cancellationToken)
    {
        return domainSpec is null
            ? ExpansionOutcome.Success(currentDomain)
            : await ExpandDomainSpecAsync(
                    context,
                    domainSpec,
                    currentDomain,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    private async ValueTask<ExpansionOutcome> ExpandDomainSpecAsync(
        EvaluationContext context,
        string domainSpec,
        string currentDomain,
        CancellationToken cancellationToken)
    {
        if (!SpfRecordParser.ValidateMacroString(
                domainSpec,
                allowExplanationOnlyMacros: false,
                out var validationDiagnostic))
        {
            return ExpansionOutcome.Error(SpfResult.PermError, validationDiagnostic);
        }

        var output = new StringBuilder(domainSpec.Length + 32);
        for (var index = 0; index < domainSpec.Length; index++)
        {
            if (domainSpec[index] != '%')
            {
                output.Append(domainSpec[index]);
                continue;
            }

            index++;
            switch (domainSpec[index])
            {
                case '%':
                    output.Append('%');
                    break;
                case '_':
                    output.Append(' ');
                    break;
                case '-':
                    output.Append("%20");
                    break;
                case '{':
                {
                    var closingBrace = domainSpec.IndexOf('}', index + 1);
                    var expression = domainSpec[(index + 1)..closingBrace];
                    var expansion = await ExpandMacroAsync(
                            context,
                            expression,
                            currentDomain,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (expansion.ErrorResult is not null)
                    {
                        return expansion;
                    }

                    output.Append(expansion.Domain);
                    index = closingBrace;
                    break;
                }
            }
        }

        var expanded = TruncateDomain(output.ToString());
        return TryValidateDomain(expanded, strictHostName: false, out var normalized)
            ? ExpansionOutcome.Success(normalized)
            : ExpansionOutcome.Error(
                SpfResult.PermError,
                $"SPF macro expansion produced malformed domain '{expanded}'.");
    }

    private async ValueTask<ExpansionOutcome> ExpandMacroAsync(
        EvaluationContext context,
        string expression,
        string currentDomain,
        CancellationToken cancellationToken)
    {
        var letter = char.ToLowerInvariant(expression[0]);
        string value;
        switch (letter)
        {
            case 's':
                value = context.Request.Sender;
                break;
            case 'l':
                value = SenderLocalPart(context.Request.Sender);
                break;
            case 'o':
                value = SenderDomain(context.Request.Sender, context.Request.Domain);
                break;
            case 'd':
                value = currentDomain;
                break;
            case 'i':
                value = FormatIpMacro(context.Request.ClientAddress);
                break;
            case 'p':
            {
                if (!context.TryConsumeDnsTerm(out var limitDiagnostic))
                {
                    return ExpansionOutcome.Error(SpfResult.PermError, limitDiagnostic);
                }

                var validated = await GetValidatedDomainsAsync(context, cancellationToken)
                    .ConfigureAwait(false);
                if (validated.ErrorResult is not null)
                {
                    return ExpansionOutcome.Error(
                        validated.ErrorResult.Value,
                        validated.Diagnostic);
                }

                value = SelectValidatedDomain(validated.Domains, currentDomain);
                break;
            }
            case 'v':
                value = context.Request.ClientAddress.AddressFamily == AddressFamily.InterNetwork
                    ? "in-addr"
                    : "ip6";
                break;
            case 'h':
                value = context.Request.HeloDomain;
                break;
            default:
                return ExpansionOutcome.Error(
                    SpfResult.PermError,
                    $"SPF macro letter '{expression[0]}' is not valid here.");
        }

        var transformed = ApplyMacroTransformers(value, expression[1..]);
        if (char.IsUpper(expression[0]))
        {
            transformed = Uri.EscapeDataString(transformed);
        }

        return ExpansionOutcome.Success(transformed);
    }

    private async ValueTask<ValidatedDomainOutcome> GetValidatedDomainsAsync(
        EvaluationContext context,
        CancellationToken cancellationToken)
    {
        if (context.ValidatedDomains is not null)
        {
            return context.ValidatedDomains;
        }

        var ptr = await QuerySafeAsync(
                token => _resolver.QueryPtrAsync(context.Request.ClientAddress, token),
                cancellationToken)
            .ConfigureAwait(false);
        if (ptr.Status == SpfDnsStatus.TemporaryError)
        {
            context.ValidatedDomains = ValidatedDomainOutcome.Success(Array.Empty<string>());
            return context.ValidatedDomains;
        }

        if (ptr.Status is SpfDnsStatus.NoData or SpfDnsStatus.NameError
            || ptr.Records.Count == 0)
        {
            if (!context.TryRegisterVoidLookup(out var limitDiagnostic))
            {
                context.ValidatedDomains = ValidatedDomainOutcome.Error(
                    SpfResult.PermError,
                    limitDiagnostic);
                return context.ValidatedDomains;
            }

            context.ValidatedDomains = ValidatedDomainOutcome.Success(Array.Empty<string>());
            return context.ValidatedDomains;
        }

        var validated = new List<string>();
        foreach (var name in ptr.Records.Take(_options.MaxPtrHosts))
        {
            if (!TryValidateDomain(name, strictHostName: false, out var normalizedName))
            {
                continue;
            }

            var addresses = await QuerySafeAsync(
                    token => _resolver.QueryAddressesAsync(
                        normalizedName,
                        context.Request.ClientAddress.AddressFamily,
                        token),
                    cancellationToken)
                .ConfigureAwait(false);
            if (addresses.Status == SpfDnsStatus.TemporaryError)
            {
                continue;
            }

            if (addresses.Status is SpfDnsStatus.NoData or SpfDnsStatus.NameError
                || addresses.Records.Count == 0)
            {
                if (!context.TryRegisterVoidLookup(out var limitDiagnostic))
                {
                    context.ValidatedDomains = ValidatedDomainOutcome.Error(
                        SpfResult.PermError,
                        limitDiagnostic);
                    return context.ValidatedDomains;
                }

                continue;
            }

            if (addresses.Records.Any(address => address.Equals(context.Request.ClientAddress)))
            {
                validated.Add(normalizedName);
            }
        }

        context.ValidatedDomains = ValidatedDomainOutcome.Success(validated);
        return context.ValidatedDomains;
    }

    private static MechanismOutcome? RegisterDnsResponse<T>(
        EvaluationContext context,
        SpfDnsResponse<T> response,
        string domain)
    {
        if (response.Status == SpfDnsStatus.TemporaryError)
        {
            return MechanismOutcome.Error(
                SpfResult.TempError,
                $"DNS lookup for '{domain}' failed temporarily.");
        }

        if (response.Status is SpfDnsStatus.NoData or SpfDnsStatus.NameError
            || response.Records.Count == 0)
        {
            return context.TryRegisterVoidLookup(out var limitDiagnostic)
                ? MechanismOutcome.NoMatch
                : MechanismOutcome.Error(SpfResult.PermError, limitDiagnostic);
        }

        return null;
    }

    private static async ValueTask<SpfDnsResponse<T>> QuerySafeAsync<T>(
        Func<CancellationToken, ValueTask<SpfDnsResponse<T>>> query,
        CancellationToken cancellationToken)
    {
        try
        {
            return await query(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return SpfDnsResponse<T>.TemporaryError();
        }
    }

    private static string ApplyMacroTransformers(string value, string transformers)
    {
        var index = 0;
        while (index < transformers.Length && char.IsAsciiDigit(transformers[index]))
        {
            index++;
        }

        int? count = null;
        if (index > 0)
        {
            count = int.Parse(transformers[..index], NumberStyles.None, CultureInfo.InvariantCulture);
        }

        var reverse = index < transformers.Length
            && char.ToLowerInvariant(transformers[index]) == 'r';
        if (reverse)
        {
            index++;
        }

        var delimiters = index < transformers.Length
            ? transformers[index..].ToCharArray()
            : ['.'];
        var parts = value.Split(delimiters, StringSplitOptions.None);
        if (reverse)
        {
            Array.Reverse(parts);
        }

        if (count is not null && parts.Length > count.Value)
        {
            parts = parts[^count.Value..];
        }

        return string.Join('.', parts);
    }

    private static string SelectValidatedDomain(
        IReadOnlyList<string> validatedDomains,
        string currentDomain)
    {
        return validatedDomains.FirstOrDefault(
                   domain => domain.Equals(currentDomain, StringComparison.OrdinalIgnoreCase))
               ?? validatedDomains.FirstOrDefault(domain => IsSameOrSubdomain(domain, currentDomain))
               ?? validatedDomains.FirstOrDefault()
               ?? "unknown";
    }

    private static bool MatchesPrefix(IPAddress left, IPAddress right, int prefixLength)
    {
        if (left.AddressFamily != right.AddressFamily)
        {
            return false;
        }

        var leftBytes = left.GetAddressBytes();
        var rightBytes = right.GetAddressBytes();
        var wholeBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var index = 0; index < wholeBytes; index++)
        {
            if (leftBytes[index] != rightBytes[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (leftBytes[wholeBytes] & mask) == (rightBytes[wholeBytes] & mask);
    }

    private static string FormatIpMacro(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return address.ToString();
        }

        var output = new char[63];
        var bytes = address.GetAddressBytes();
        var outputIndex = 0;
        foreach (var value in bytes)
        {
            output[outputIndex++] = ToHex(value >> 4);
            output[outputIndex++] = '.';
            output[outputIndex++] = ToHex(value & 0x0F);
            output[outputIndex++] = '.';
        }

        return new string(output, 0, outputIndex - 1);
    }

    private static char ToHex(int value) =>
        (char)(value < 10 ? '0' + value : 'a' + value - 10);

    private static string NormalizeSender(string sender, string domain)
    {
        sender ??= string.Empty;
        var atIndex = sender.LastIndexOf('@');
        if (atIndex <= 0 || atIndex == sender.Length - 1)
        {
            return "postmaster@" + domain;
        }

        return sender;
    }

    private static string SenderLocalPart(string sender)
    {
        var atIndex = sender.LastIndexOf('@');
        return atIndex > 0 ? sender[..atIndex] : "postmaster";
    }

    private static string SenderDomain(string sender, string fallback)
    {
        var atIndex = sender.LastIndexOf('@');
        return atIndex >= 0 && atIndex < sender.Length - 1
            ? sender[(atIndex + 1)..]
            : fallback;
    }

    private static bool TryValidateDomain(
        string value,
        bool strictHostName,
        out string normalized)
    {
        normalized = NormalizeDomain(value);
        if (normalized.Length is 0 or > 253
            || normalized.Any(static character => character > 0x7F))
        {
            return false;
        }

        var labels = normalized.Split('.');
        if (labels.Length < 2
            || labels.Any(static label => label.Length is 0 or > 63))
        {
            return false;
        }

        if (!IsTopLabel(labels[^1]))
        {
            return false;
        }

        return strictHostName
            ? labels.All(IsHostLabel)
            : labels.All(IsExpandedDnsLabel);
    }

    private static bool IsTopLabel(string label) =>
        IsHostLabel(label)
        && label.Any(char.IsAsciiLetter);

    private static bool IsHostLabel(string label) =>
        char.IsAsciiLetterOrDigit(label[0])
        && char.IsAsciiLetterOrDigit(label[^1])
        && label.All(
            static character =>
                char.IsAsciiLetterOrDigit(character)
                || character == '-');

    private static bool IsExpandedDnsLabel(string label) =>
        label.All(
            static character =>
                character is >= (char)0x21 and <= (char)0x7E
                && character is not '/' and not '\\' and not ':' and not '[' and not ']');

    private static string NormalizeDomain(string? value) =>
        (value ?? string.Empty).TrimEnd('.').ToLowerInvariant();

    private static string TruncateDomain(string value)
    {
        var normalized = NormalizeDomain(value);
        while (normalized.Length > 253)
        {
            var dot = normalized.IndexOf('.');
            if (dot < 0)
            {
                break;
            }

            normalized = normalized[(dot + 1)..];
        }

        return normalized;
    }

    private static bool IsSameOrSubdomain(string candidate, string domain) =>
        candidate.Equals(domain, StringComparison.OrdinalIgnoreCase)
        || candidate.EndsWith('.' + domain, StringComparison.OrdinalIgnoreCase);

    private static bool IsSpfPolicy(string value)
    {
        const string version = "v=spf1";
        return value.StartsWith(version, StringComparison.OrdinalIgnoreCase)
            && (value.Length == version.Length || value[version.Length] == ' ');
    }

    private static SpfResult QualifierToResult(SpfQualifier qualifier) =>
        qualifier switch
        {
            SpfQualifier.Pass => SpfResult.Pass,
            SpfQualifier.Fail => SpfResult.Fail,
            SpfQualifier.SoftFail => SpfResult.SoftFail,
            SpfQualifier.Neutral => SpfResult.Neutral,
            _ => SpfResult.PermError
        };

    private static void ValidateOptions(SpfEvaluatorOptions options)
    {
        if (options.MaxDnsTerms is <= 0 or > 10
            || options.MaxVoidLookups < 0
            || options.MaxMxHosts is <= 0 or > 10
            || options.MaxPtrHosts is <= 0 or > 10
            || options.MaxRecursionDepth is < 0 or > 10
            || options.EvaluationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "SPF DNS, MX, PTR, and recursion limits cannot exceed the RFC limit of 10; timeout must be positive.");
        }
    }

    private sealed class EvaluationContext(
        SpfEvaluationRequest request,
        SpfEvaluatorOptions options)
    {
        public SpfEvaluationRequest Request { get; } = request;

        public HashSet<string> ActiveDomains { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int DnsTermCount { get; private set; }

        public int VoidLookupCount { get; private set; }

        public ValidatedDomainOutcome? ValidatedDomains { get; set; }

        public bool TryConsumeDnsTerm(out string diagnostic)
        {
            DnsTermCount++;
            diagnostic = DnsTermCount > options.MaxDnsTerms
                ? "SPF evaluation exceeded its DNS-producing term limit."
                : string.Empty;
            return diagnostic.Length == 0;
        }

        public bool TryRegisterVoidLookup(out string diagnostic)
        {
            VoidLookupCount++;
            diagnostic = VoidLookupCount > options.MaxVoidLookups
                ? "SPF evaluation exceeded its void DNS lookup limit."
                : string.Empty;
            return diagnostic.Length == 0;
        }

        public SpfEvaluation Build(
            SpfResult result,
            string domain,
            string? matchedMechanism,
            string diagnostic) =>
            new(
                result,
                domain,
                matchedMechanism,
                DnsTermCount,
                VoidLookupCount,
                diagnostic);
    }

    private sealed record DomainOutcome(
        SpfResult Result,
        string? MatchedMechanism,
        string Diagnostic)
    {
        public static DomainOutcome Error(SpfResult result, string diagnostic) =>
            new(result, null, diagnostic);
    }

    private sealed record MechanismOutcome(
        bool Matched,
        SpfResult? ErrorResult,
        string Diagnostic)
    {
        public static MechanismOutcome Match { get; } = new(true, null, string.Empty);

        public static MechanismOutcome NoMatch { get; } = new(false, null, string.Empty);

        public static MechanismOutcome FromMatch(bool matched) =>
            matched ? Match : NoMatch;

        public static MechanismOutcome Error(SpfResult result, string diagnostic) =>
            new(false, result, diagnostic);
    }

    private sealed record ExpansionOutcome(
        string? Domain,
        SpfResult? ErrorResult,
        string Diagnostic)
    {
        public static ExpansionOutcome Success(string value) =>
            new(value, null, string.Empty);

        public static ExpansionOutcome Error(SpfResult result, string diagnostic) =>
            new(null, result, diagnostic);
    }

    private sealed record ValidatedDomainOutcome(
        IReadOnlyList<string> Domains,
        SpfResult? ErrorResult,
        string Diagnostic)
    {
        public static ValidatedDomainOutcome Success(IReadOnlyList<string> domains) =>
            new(domains, null, string.Empty);

        public static ValidatedDomainOutcome Error(SpfResult result, string diagnostic) =>
            new(Array.Empty<string>(), result, diagnostic);
    }
}
