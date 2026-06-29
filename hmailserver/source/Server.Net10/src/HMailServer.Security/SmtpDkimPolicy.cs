using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class SmtpDkimPolicy : ISmtpDkimPolicy
{
    private readonly IDkimTxtResolver _resolver;
    private readonly SmtpDkimPolicyOptions _options;

    public SmtpDkimPolicy(
        IDkimTxtResolver resolver,
        SmtpDkimPolicyOptions? options = null)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _options = options ?? new SmtpDkimPolicyOptions();
    }

    public async ValueTask<SmtpDkimPolicyResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled
            || !request.EnableSpamScan
            || (_options.SkipAuthenticated && request.IsAuthenticated)
            || request.MessageData.Length == 0)
        {
            return SmtpDkimPolicyResult.Skipped;
        }

        try
        {
            var message = Encoding.Latin1.GetString(request.MessageData);
            var evaluation = await DkimMessageVerifier
                .VerifyAsync(message, _resolver, cancellationToken)
                .ConfigureAwait(false);
            return SmtpDkimPolicyResult.FromEvaluation(
                MapStatus(evaluation.Result),
                _options.FailureScore,
                evaluation.Diagnostic,
                evaluation.PassingDomains);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return SmtpDkimPolicyResult.FromEvaluation(
                SmtpDkimPolicyStatus.TempFail,
                _options.FailureScore,
                "DKIM policy evaluation failed open: " + ex.GetType().Name);
        }
    }

    private static SmtpDkimPolicyStatus MapStatus(DkimResult result) =>
        result switch
        {
            DkimResult.Neutral => SmtpDkimPolicyStatus.Neutral,
            DkimResult.Pass => SmtpDkimPolicyStatus.Pass,
            DkimResult.TempFail => SmtpDkimPolicyStatus.TempFail,
            DkimResult.PermFail => SmtpDkimPolicyStatus.PermFail,
            _ => SmtpDkimPolicyStatus.TempFail
        };
}
