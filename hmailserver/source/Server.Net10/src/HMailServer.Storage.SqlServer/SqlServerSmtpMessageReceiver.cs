using HMailServer.Core.Abstractions;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSmtpMessageReceiver : ISmtpMessageReceiver
{
    public const string InsertQueuedMessageSql = SqlServerSmtpQueueWriter.InsertQueuedMessageSql;

    public const string InsertRecipientSql = SqlServerSmtpQueueWriter.InsertRecipientSql;

    public const string UnlockQueuedMessageSql = SqlServerSmtpQueueWriter.UnlockQueuedMessageSql;

    private readonly ISmtpQueueWriter _queueWriter;
    private readonly ISmtpRuleProcessor? _ruleProcessor;
    private readonly ISmtpEventScriptExecutor? _eventScriptExecutor;
    private readonly IMessageAntivirusScanner? _antivirusScanner;
    private readonly IMessageSpamScanner? _spamScanner;
    private readonly IMessageSpamPolicy? _spamPolicy;
    private readonly IMessageAttachmentPolicy? _attachmentPolicy;
    private readonly ISmtpDnsBlockListChecker? _dnsBlockListChecker;
    private readonly ISmtpReverseDnsChecker? _reverseDnsChecker;
    private readonly ISmtpSenderDomainMxChecker? _senderDomainMxChecker;
    private readonly ISmtpSpfPolicy? _spfPolicy;
    private readonly ISmtpDkimPolicy? _dkimPolicy;
    private readonly ISmtpDmarcPolicy? _dmarcPolicy;
    private readonly ISmtpGreylistingChecker? _greylistingChecker;
    private readonly SmtpGreylistingOptions _greylistingOptions;
    private readonly ISmtpUrlBlockListChecker? _urlBlockListChecker;
    private readonly ServerStatusRuntimeState? _statusRuntimeState;

    public SqlServerSmtpMessageReceiver(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver,
        ISmtpRuleProcessor? ruleProcessor = null,
        ISmtpEventScriptExecutor? eventScriptExecutor = null,
        ISmtpQueueWriter? queueWriter = null,
        IMessageAntivirusScanner? antivirusScanner = null,
        IMessageSpamScanner? spamScanner = null,
        IMessageSpamPolicy? spamPolicy = null,
        IMessageAttachmentPolicy? attachmentPolicy = null,
        ISmtpDnsBlockListChecker? dnsBlockListChecker = null,
        ISmtpReverseDnsChecker? reverseDnsChecker = null,
        ISmtpSenderDomainMxChecker? senderDomainMxChecker = null,
        ISmtpSpfPolicy? spfPolicy = null,
        ISmtpDkimPolicy? dkimPolicy = null,
        ISmtpDmarcPolicy? dmarcPolicy = null,
        ISmtpGreylistingChecker? greylistingChecker = null,
        SmtpGreylistingOptions? greylistingOptions = null,
        ISmtpUrlBlockListChecker? urlBlockListChecker = null,
        ServerStatusRuntimeState? statusRuntimeState = null)
    {
        _queueWriter = queueWriter ?? new SqlServerSmtpQueueWriter(connectionFactory, pathResolver);
        _ruleProcessor = ruleProcessor;
        _eventScriptExecutor = eventScriptExecutor;
        _antivirusScanner = antivirusScanner;
        _spamScanner = spamScanner;
        _spamPolicy = spamPolicy;
        _attachmentPolicy = attachmentPolicy;
        _dnsBlockListChecker = dnsBlockListChecker;
        _reverseDnsChecker = reverseDnsChecker;
        _senderDomainMxChecker = senderDomainMxChecker;
        _spfPolicy = spfPolicy;
        _dkimPolicy = dkimPolicy;
        _dmarcPolicy = dmarcPolicy;
        _greylistingChecker = greylistingChecker;
        _greylistingOptions = greylistingOptions ?? new SmtpGreylistingOptions();
        _urlBlockListChecker = urlBlockListChecker;
        _statusRuntimeState = statusRuntimeState;
    }

    public async ValueTask<SmtpReceiveResult> ReceiveAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Recipients.Count == 0)
        {
            return SmtpReceiveResult.Failure("554 No valid recipients");
        }

        var dnsBlockListFailure = await RunDnsBlockListCheckAsync(request, cancellationToken).ConfigureAwait(false);
        if (dnsBlockListFailure is not null)
        {
            return dnsBlockListFailure;
        }

        var reverseDnsFailure = await RunReverseDnsCheckAsync(request, cancellationToken).ConfigureAwait(false);
        if (reverseDnsFailure is not null)
        {
            return reverseDnsFailure;
        }

        var senderDomainMxFailure = await RunSenderDomainMxCheckAsync(request, cancellationToken).ConfigureAwait(false);
        if (senderDomainMxFailure is not null)
        {
            return senderDomainMxFailure;
        }

        var spfPolicyResult = await RunSpfPolicyAsync(request, cancellationToken).ConfigureAwait(false);

        var greylistingFailure = await RunGreylistingCheckAsync(request, spfPolicyResult, cancellationToken).ConfigureAwait(false);
        if (greylistingFailure is not null)
        {
            return greylistingFailure;
        }

        if (_eventScriptExecutor is not null)
        {
            var eventResult = _eventScriptExecutor.Execute(
                new SmtpEventScriptExecutionRequest(
                    "OnAcceptMessage",
                    new SmtpEventScriptClient(
                        request.AuthenticatedUsername,
                        request.ClientIPAddress,
                        request.ClientPort,
                        request.SessionId,
                        request.HeloHost,
                        request.IsAuthenticated,
                        request.IsEncryptedConnection),
                    request.MailFrom,
                    request.Recipients,
                    request.MessageData),
                cancellationToken);
            if (!eventResult.Accepted)
            {
                return SmtpReceiveResult.Failure(
                    string.IsNullOrWhiteSpace(eventResult.FailureResponse)
                        ? "451 Requested action aborted: local error in processing"
                        : eventResult.FailureResponse);
            }

            if (eventResult.DropMessage)
            {
                return SmtpReceiveResult.Success();
            }

            if (eventResult.MessageData is not null)
            {
                request = request with { MessageData = eventResult.MessageData };
            }
        }

        var spamScanResult = await RunSpamScanAsync(request, cancellationToken).ConfigureAwait(false);
        if (spamScanResult.FailureResult is not null)
        {
            return spamScanResult.FailureResult;
        }

        request = spamScanResult.Request with
        {
            OriginalMessageSpamFlagged = request.OriginalMessageSpamFlagged == true
                || (spamScanResult.MessageFlags & SmtpQueueWriteRequest.SpamFlag) != 0
        };
        var messageFlags = spamScanResult.MessageFlags;

        var ruleForcedRouteId = 0;
        string? ruleBindAddress = null;
        if (_ruleProcessor is not null)
        {
            var ruleResult = await _ruleProcessor.ProcessAsync(request, cancellationToken).ConfigureAwait(false);
            if (!ruleResult.Accepted)
            {
                return SmtpReceiveResult.Failure(
                    string.IsNullOrWhiteSpace(ruleResult.FailureResponse)
                        ? "451 Requested action aborted: local error in processing"
                        : ruleResult.FailureResponse);
            }

            if (ruleResult.DropMessage)
            {
                await EnqueueGeneratedMessagesAsync(ruleResult, request.ReceivedUtc, cancellationToken).ConfigureAwait(false);
                return SmtpReceiveResult.Success();
            }

            await EnqueueGeneratedMessagesAsync(ruleResult, request.ReceivedUtc, cancellationToken).ConfigureAwait(false);
            request = request with { MessageData = ruleResult.MessageData };
            ruleForcedRouteId = ruleResult.ForcedRouteId;
            ruleBindAddress = ruleResult.BindToAddress;
        }

        var dkimPolicyResult = await RunDkimPolicyAsync(request, cancellationToken).ConfigureAwait(false);
        var dmarcPolicyResult = await RunDmarcPolicyAsync(
                request,
                spfPolicyResult,
                dkimPolicyResult,
                cancellationToken)
            .ConfigureAwait(false);

        if (spfPolicyResult.MarkAsSpam)
        {
            if ((messageFlags & SmtpQueueWriteRequest.SpamFlag) == 0)
            {
                _statusRuntimeState?.OnSpamMessageDetected();
            }

            messageFlags |= SmtpQueueWriteRequest.SpamFlag;
        }

        if (dkimPolicyResult.MarkAsSpam)
        {
            if ((messageFlags & SmtpQueueWriteRequest.SpamFlag) == 0)
            {
                _statusRuntimeState?.OnSpamMessageDetected();
            }

            messageFlags |= SmtpQueueWriteRequest.SpamFlag;
        }

        if (dmarcPolicyResult.MarkAsSpam)
        {
            if ((messageFlags & SmtpQueueWriteRequest.SpamFlag) == 0)
            {
                _statusRuntimeState?.OnSpamMessageDetected();
            }

            messageFlags |= SmtpQueueWriteRequest.SpamFlag;
        }

        request = await RunAttachmentPolicyAsync(request, cancellationToken).ConfigureAwait(false);

        var urlBlockListFailure = await RunUrlBlockListCheckAsync(request, cancellationToken).ConfigureAwait(false);
        if (urlBlockListFailure is not null)
        {
            return urlBlockListFailure;
        }

        var antivirusFailure = await RunAntivirusScanAsync(request, cancellationToken).ConfigureAwait(false);
        if (antivirusFailure is not null)
        {
            return antivirusFailure;
        }

        try
        {
            await _queueWriter
                .EnqueueAsync(
                    new SmtpQueueWriteRequest(
                        request.MailFrom,
                        request.Recipients,
                        request.MessageData,
                        request.ReceivedUtc,
                        ruleForcedRouteId,
                        ruleBindAddress,
                        messageFlags),
                    cancellationToken)
                .ConfigureAwait(false);
            return SmtpReceiveResult.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SmtpReceiveResult.Failure("451 Requested action aborted: local error in processing");
        }
    }

    private async ValueTask<SmtpReceiveResult?> RunDnsBlockListCheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if (_dnsBlockListChecker is null)
        {
            return null;
        }

        try
        {
            var result = await _dnsBlockListChecker
                .CheckAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return result.Listed
                ? SmtpReceiveResult.Failure(
                    string.IsNullOrWhiteSpace(result.FailureResponse)
                        ? "554 Rejected by DNS blocklist"
                        : result.FailureResponse)
                : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async ValueTask<SmtpReceiveResult?> RunReverseDnsCheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if (_reverseDnsChecker is null)
        {
            return null;
        }

        try
        {
            var result = await _reverseDnsChecker
                .CheckAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return result.Rejected
                ? SmtpReceiveResult.Failure(
                    string.IsNullOrWhiteSpace(result.FailureResponse)
                        ? "554 Rejected by reverse DNS check"
                        : result.FailureResponse)
                : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async ValueTask<SmtpReceiveResult?> RunSenderDomainMxCheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if (_senderDomainMxChecker is null)
        {
            return null;
        }

        try
        {
            var result = await _senderDomainMxChecker
                .CheckAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return result.Rejected
                ? SmtpReceiveResult.Failure(
                    string.IsNullOrWhiteSpace(result.FailureResponse)
                        ? "554 Sender domain does not have any MX records"
                        : result.FailureResponse)
                : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async ValueTask<SmtpReceiveResult?> RunGreylistingCheckAsync(
        SmtpReceiveRequest request,
        SmtpSpfPolicyResult spfPolicyResult,
        CancellationToken cancellationToken)
    {
        if (_greylistingChecker is null)
        {
            return null;
        }

        if (_greylistingOptions.BypassOnSpfPass && spfPolicyResult.Passed)
        {
            return null;
        }

        try
        {
            var result = await _greylistingChecker
                .CheckAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return result.Deferred
                ? SmtpReceiveResult.Failure(
                    string.IsNullOrWhiteSpace(result.FailureResponse)
                        ? "451 Please try again later."
                        : result.FailureResponse)
                : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async ValueTask<SmtpSpfPolicyResult> RunSpfPolicyAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if (_spfPolicy is null)
        {
            return SmtpSpfPolicyResult.Skipped;
        }

        try
        {
            return await _spfPolicy
                .CheckAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return SmtpSpfPolicyResult.Skipped;
        }
    }

    private async ValueTask<SmtpDkimPolicyResult> RunDkimPolicyAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if (_dkimPolicy is null)
        {
            return SmtpDkimPolicyResult.Skipped;
        }

        try
        {
            return await _dkimPolicy
                .CheckAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return SmtpDkimPolicyResult.Skipped;
        }
    }

    private async ValueTask<SmtpDmarcPolicyResult> RunDmarcPolicyAsync(
        SmtpReceiveRequest request,
        SmtpSpfPolicyResult spfPolicyResult,
        SmtpDkimPolicyResult dkimPolicyResult,
        CancellationToken cancellationToken)
    {
        if (_dmarcPolicy is null)
        {
            return SmtpDmarcPolicyResult.Skipped;
        }

        try
        {
            return await _dmarcPolicy
                .CheckAsync(request, spfPolicyResult, dkimPolicyResult, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return SmtpDmarcPolicyResult.Skipped;
        }
    }

    private async ValueTask<SpamScanApplicationResult> RunSpamScanAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if (_spamScanner is null || !request.EnableSpamScan)
        {
            return new SpamScanApplicationResult(request, SmtpQueueWriteRequest.RecentFlag);
        }

        try
        {
            var scanResult = await _spamScanner
                .ScanAsync(request.MessageData, request.MailFrom, cancellationToken)
                .ConfigureAwait(false);
            if (scanResult.MessageData.Length == 0)
            {
                return new SpamScanApplicationResult(request, SmtpQueueWriteRequest.RecentFlag);
            }

            var messageData = scanResult.MessageData;
            var messageFlags = SmtpQueueWriteRequest.RecentFlag;
            if (_spamPolicy is not null)
            {
                var policyResult = _spamPolicy.Apply(messageData, scanResult);
                messageData = policyResult.MessageData;
                if (policyResult.RejectMessage)
                {
                    _statusRuntimeState?.OnSpamMessageDetected();
                    return new SpamScanApplicationResult(
                        request with { MessageData = messageData },
                        SmtpQueueWriteRequest.RecentFlag,
                        SmtpReceiveResult.Failure(
                            string.IsNullOrWhiteSpace(policyResult.FailureResponse)
                                ? "554 Message rejected as spam"
                                : policyResult.FailureResponse));
                }

                if (policyResult.MarkAsSpam)
                {
                    _statusRuntimeState?.OnSpamMessageDetected();
                    messageFlags |= SmtpQueueWriteRequest.SpamFlag;
                }
            }

            return new SpamScanApplicationResult(request with { MessageData = messageData }, messageFlags);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new SpamScanApplicationResult(request, SmtpQueueWriteRequest.RecentFlag);
        }
    }

    private async ValueTask<SmtpReceiveRequest> RunAttachmentPolicyAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if (_attachmentPolicy is null)
        {
            return request;
        }

        try
        {
            var result = await _attachmentPolicy
                .ApplyAsync(request.MessageData, cancellationToken)
                .ConfigureAwait(false);
            return result.Modified
                ? request with { MessageData = result.MessageData }
                : request;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return request;
        }
    }

    private async ValueTask<SmtpReceiveResult?> RunUrlBlockListCheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if (_urlBlockListChecker is null)
        {
            return null;
        }

        try
        {
            var result = await _urlBlockListChecker
                .CheckAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return result.Listed
                ? SmtpReceiveResult.Failure(
                    string.IsNullOrWhiteSpace(result.FailureResponse)
                        ? "554 Rejected by URL blocklist"
                        : result.FailureResponse)
                : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async ValueTask<SmtpReceiveResult?> RunAntivirusScanAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if (_antivirusScanner is null || !request.EnableAntivirusScan)
        {
            return null;
        }

        MessageAntivirusScanResult scanResult;
        try
        {
            scanResult = await _antivirusScanner
                .ScanAsync(request.MessageData, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return SmtpReceiveResult.Failure("451 Requested action aborted: antivirus scan failed");
        }

        if (!scanResult.Succeeded)
        {
            return SmtpReceiveResult.Failure("451 Requested action aborted: antivirus scan failed");
        }

        if (scanResult.IsInfected)
        {
            _statusRuntimeState?.OnVirusRemoved();
            return SmtpReceiveResult.Failure(BuildVirusDetectedResponse(scanResult.VirusName));
        }

        return null;
    }

    private static string BuildVirusDetectedResponse(string virusName)
    {
        if (string.IsNullOrWhiteSpace(virusName))
        {
            return "554 Virus detected";
        }

        return "554 Virus detected: " + virusName.Replace('\r', ' ').Replace('\n', ' ');
    }

    private async ValueTask EnqueueGeneratedMessagesAsync(
        SmtpRuleProcessingResult ruleResult,
        DateTimeOffset receivedUtc,
        CancellationToken cancellationToken)
    {
        foreach (var message in ruleResult.GeneratedMessages)
        {
            await _queueWriter
                .EnqueueAsync(
                    new SmtpQueueWriteRequest(
                        message.MailFrom,
                        message.Recipients,
                        message.MessageData,
                        receivedUtc),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private sealed record SpamScanApplicationResult(
        SmtpReceiveRequest Request,
        byte MessageFlags,
        SmtpReceiveResult? FailureResult = null);
}
