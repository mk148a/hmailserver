using System.Globalization;
using System.Net;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class SmtpSpfPolicy : ISmtpSpfPolicy
{
    private readonly SpfEvaluator _evaluator;
    private readonly SmtpSpfPolicyOptions _options;

    public SmtpSpfPolicy(
        SpfEvaluator evaluator,
        SmtpSpfPolicyOptions? options = null)
    {
        _evaluator = evaluator;
        _options = options ?? new SmtpSpfPolicyOptions();
    }

    public async ValueTask<SmtpSpfPolicyResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled
            || !request.EnableSpamScan
            || (_options.SkipAuthenticated && request.IsAuthenticated)
            || !IPAddress.TryParse(request.ClientIPAddress, out var clientAddress)
            || IsAnyAddress(clientAddress))
        {
            return SmtpSpfPolicyResult.Skipped;
        }

        var identity = BuildIdentity(request.MailFrom, request.HeloHost);
        if (identity is null)
        {
            return SmtpSpfPolicyResult.Skipped;
        }

        try
        {
            var evaluation = await _evaluator
                .EvaluateAsync(
                    new SpfEvaluationRequest(
                        clientAddress,
                        identity.Domain,
                        identity.Sender,
                        identity.HeloDomain),
                    cancellationToken)
                .ConfigureAwait(false);

            return SmtpSpfPolicyResult.FromEvaluation(
                MapStatus(evaluation.Result),
                _options.FailScore,
                evaluation.Domain,
                identity.Sender,
                identity.HeloDomain,
                evaluation.MatchedMechanism,
                evaluation.Diagnostic);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return SmtpSpfPolicyResult.FromEvaluation(
                SmtpSpfPolicyStatus.TempError,
                _options.FailScore,
                identity.Domain,
                identity.Sender,
                identity.HeloDomain,
                matchedMechanism: null,
                "SPF policy evaluation failed open: " + ex.GetType().Name);
        }
    }

    private static SpfIdentity? BuildIdentity(string mailFrom, string heloHost)
    {
        var heloDomain = NormalizeDomain(heloHost);
        var sender = mailFrom.Trim();
        if (sender.Length >= 2 && sender[0] == '<' && sender[^1] == '>')
        {
            sender = sender[1..^1].Trim();
        }

        if (sender.Length == 0)
        {
            return heloDomain.Length == 0
                ? null
                : new SpfIdentity(heloDomain, "postmaster@" + heloDomain, heloDomain);
        }

        var atIndex = sender.LastIndexOf('@');
        if (atIndex < 0 || atIndex == sender.Length - 1)
        {
            return null;
        }

        var domain = NormalizeDomain(sender[(atIndex + 1)..]);
        if (domain.Length == 0)
        {
            return null;
        }

        var localPart = sender[..atIndex];
        if (localPart.Length == 0)
        {
            localPart = "postmaster";
        }

        return new SpfIdentity(
            domain,
            localPart + "@" + domain,
            heloDomain.Length == 0 ? domain : heloDomain);
    }

    private static string NormalizeDomain(string value)
    {
        var domain = value.Trim().TrimEnd('.');
        if (domain.Length == 0 || IsDomainLiteral(domain) || IPAddress.TryParse(domain, out _))
        {
            return string.Empty;
        }

        try
        {
            domain = new IdnMapping().GetAscii(domain).TrimEnd('.');
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }

        if (domain.Length is 0 or > 253)
        {
            return string.Empty;
        }

        var labels = domain.Split('.');
        if (labels.Length < 2
            || labels.Any(static label => label.Length is 0 or > 63)
            || labels[^1].All(char.IsAsciiDigit))
        {
            return string.Empty;
        }

        return domain.ToLowerInvariant();
    }

    private static bool IsDomainLiteral(string domain)
    {
        if (domain.Length < 3 || domain[0] != '[' || domain[^1] != ']')
        {
            return false;
        }

        var literal = domain[1..^1];
        if (literal.StartsWith("IPv6:", StringComparison.OrdinalIgnoreCase))
        {
            literal = literal["IPv6:".Length..];
        }

        return IPAddress.TryParse(literal, out _);
    }

    private static bool IsAnyAddress(IPAddress address) =>
        address.Equals(IPAddress.Any)
        || address.Equals(IPAddress.IPv6Any);

    private static SmtpSpfPolicyStatus MapStatus(SpfResult result) =>
        result switch
        {
            SpfResult.None => SmtpSpfPolicyStatus.None,
            SpfResult.Neutral => SmtpSpfPolicyStatus.Neutral,
            SpfResult.Pass => SmtpSpfPolicyStatus.Pass,
            SpfResult.Fail => SmtpSpfPolicyStatus.Fail,
            SpfResult.SoftFail => SmtpSpfPolicyStatus.SoftFail,
            SpfResult.TempError => SmtpSpfPolicyStatus.TempError,
            SpfResult.PermError => SmtpSpfPolicyStatus.PermError,
            _ => SmtpSpfPolicyStatus.TempError
        };

    private sealed record SpfIdentity(
        string Domain,
        string Sender,
        string HeloDomain);
}
