using HMailServer.Core.Abstractions;
using MimeKit;

namespace HMailServer.Security;

public sealed class SmtpDmarcPolicy : ISmtpDmarcPolicy
{
    private readonly IDmarcTxtResolver _resolver;
    private readonly SmtpDmarcPolicyOptions _options;
    private readonly IDmarcOrganizationalDomainResolver? _organizationalDomainResolver;

    public SmtpDmarcPolicy(
        IDmarcTxtResolver resolver,
        SmtpDmarcPolicyOptions? options = null,
        IDmarcOrganizationalDomainResolver? organizationalDomainResolver = null)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _options = options ?? new SmtpDmarcPolicyOptions();
        _organizationalDomainResolver = organizationalDomainResolver;
    }

    public async ValueTask<SmtpDmarcPolicyResult> CheckAsync(
        SmtpReceiveRequest request,
        SmtpSpfPolicyResult spfPolicyResult,
        SmtpDkimPolicyResult dkimPolicyResult,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(spfPolicyResult);
        ArgumentNullException.ThrowIfNull(dkimPolicyResult);

        if (!_options.Enabled
            || !request.EnableSpamScan
            || (_options.SkipAuthenticated && request.IsAuthenticated)
            || request.MessageData.Length == 0)
        {
            return SmtpDmarcPolicyResult.Skipped;
        }

        var headerFromDomain = ExtractHeaderFromDomain(request.MessageData);
        if (headerFromDomain.Length == 0)
        {
            return SmtpDmarcPolicyResult.FromEvaluation(
                SmtpDmarcPolicyStatus.PermError,
                SmtpDmarcAppliedPolicy.None,
                markFailuresAsSpam: false,
                failureScore: 0,
                string.Empty,
                "DMARC policy evaluation failed open: the RFC5322.From domain could not be determined.");
        }

        try
        {
            var organizationalDomain = await ResolveOrganizationalDomainAsync(
                    headerFromDomain,
                    cancellationToken)
                .ConfigureAwait(false);
            var evaluation = await DmarcEvaluator
                .EvaluateAsync(
                    new DmarcEvaluationRequest(
                        headerFromDomain,
                        BuildSpfResult(spfPolicyResult),
                        BuildDkimResults(dkimPolicyResult),
                        organizationalDomain),
                    _resolver,
                    cancellationToken)
                .ConfigureAwait(false);

            return SmtpDmarcPolicyResult.FromEvaluation(
                MapStatus(evaluation.Result),
                MapAppliedPolicy(evaluation.AppliedPolicy),
                _options.MarkPolicyFailuresAsSpam,
                _options.FailureScore,
                evaluation.Domain,
                evaluation.Diagnostic);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return SmtpDmarcPolicyResult.FromEvaluation(
                SmtpDmarcPolicyStatus.TempError,
                SmtpDmarcAppliedPolicy.None,
                markFailuresAsSpam: false,
                failureScore: 0,
                headerFromDomain,
                "DMARC policy evaluation failed open: " + ex.GetType().Name);
        }
    }

    private async ValueTask<string?> ResolveOrganizationalDomainAsync(
        string headerFromDomain,
        CancellationToken cancellationToken)
    {
        if (_organizationalDomainResolver is null)
        {
            return null;
        }

        try
        {
            return await _organizationalDomainResolver
                .ResolveAsync(headerFromDomain, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static DmarcSpfAuthenticationResult? BuildSpfResult(SmtpSpfPolicyResult result) =>
        result.Evaluated && result.Domain.Length > 0
            ? new DmarcSpfAuthenticationResult(result.Passed, result.Domain)
            : null;

    private static IReadOnlyList<DmarcDkimAuthenticationResult> BuildDkimResults(
        SmtpDkimPolicyResult result) =>
        result.PassingDomains
            .Where(static domain => !string.IsNullOrWhiteSpace(domain))
            .Select(static domain => new DmarcDkimAuthenticationResult(true, domain))
            .ToArray();

    private static string ExtractHeaderFromDomain(byte[] messageData)
    {
        try
        {
            using var stream = new MemoryStream(messageData, writable: false);
            var message = MimeMessage.Load(stream);
            var mailboxes = message.From.Mailboxes.ToArray();
            if (mailboxes.Length != 1)
            {
                return string.Empty;
            }

            var address = mailboxes[0].Address.Trim();
            var atIndex = address.LastIndexOf('@');
            return atIndex < 0 || atIndex == address.Length - 1
                ? string.Empty
                : address[(atIndex + 1)..].Trim();
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static SmtpDmarcPolicyStatus MapStatus(DmarcResult result) =>
        result switch
        {
            DmarcResult.None => SmtpDmarcPolicyStatus.None,
            DmarcResult.Pass => SmtpDmarcPolicyStatus.Pass,
            DmarcResult.Fail => SmtpDmarcPolicyStatus.Fail,
            DmarcResult.TempError => SmtpDmarcPolicyStatus.TempError,
            DmarcResult.PermError => SmtpDmarcPolicyStatus.PermError,
            _ => SmtpDmarcPolicyStatus.TempError
        };

    private static SmtpDmarcAppliedPolicy MapAppliedPolicy(DmarcPolicy policy) =>
        policy switch
        {
            DmarcPolicy.None => SmtpDmarcAppliedPolicy.None,
            DmarcPolicy.Quarantine => SmtpDmarcAppliedPolicy.Quarantine,
            DmarcPolicy.Reject => SmtpDmarcAppliedPolicy.Reject,
            _ => SmtpDmarcAppliedPolicy.None
        };
}
