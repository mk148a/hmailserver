using HMailServer.Core.Abstractions;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSmtpMessageReceiver : ISmtpMessageReceiver
{
    public const string InsertQueuedMessageSql = SqlServerSmtpQueueWriter.InsertQueuedMessageSql;

    public const string InsertRecipientSql = SqlServerSmtpQueueWriter.InsertRecipientSql;

    public const string UnlockQueuedMessageSql = SqlServerSmtpQueueWriter.UnlockQueuedMessageSql;

    private readonly SqlServerSmtpQueueWriter _queueWriter;
    private readonly ISmtpRuleProcessor? _ruleProcessor;
    private readonly ISmtpEventScriptExecutor? _eventScriptExecutor;
    private readonly IMessageAntivirusScanner? _antivirusScanner;
    private readonly IMessageSpamScanner? _spamScanner;
    private readonly IMessageSpamPolicy? _spamPolicy;
    private readonly IMessageAttachmentPolicy? _attachmentPolicy;
    private readonly ISmtpDnsBlockListChecker? _dnsBlockListChecker;

    public SqlServerSmtpMessageReceiver(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver,
        ISmtpRuleProcessor? ruleProcessor = null,
        ISmtpEventScriptExecutor? eventScriptExecutor = null,
        SqlServerSmtpQueueWriter? queueWriter = null,
        IMessageAntivirusScanner? antivirusScanner = null,
        IMessageSpamScanner? spamScanner = null,
        IMessageSpamPolicy? spamPolicy = null,
        IMessageAttachmentPolicy? attachmentPolicy = null,
        ISmtpDnsBlockListChecker? dnsBlockListChecker = null)
    {
        _queueWriter = queueWriter ?? new SqlServerSmtpQueueWriter(connectionFactory, pathResolver);
        _ruleProcessor = ruleProcessor;
        _eventScriptExecutor = eventScriptExecutor;
        _antivirusScanner = antivirusScanner;
        _spamScanner = spamScanner;
        _spamPolicy = spamPolicy;
        _attachmentPolicy = attachmentPolicy;
        _dnsBlockListChecker = dnsBlockListChecker;
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

        var spamScanResult = await RunSpamScanAsync(request, cancellationToken).ConfigureAwait(false);
        if (spamScanResult.FailureResult is not null)
        {
            return spamScanResult.FailureResult;
        }

        request = spamScanResult.Request;
        var messageFlags = spamScanResult.MessageFlags;

        request = await RunAttachmentPolicyAsync(request, cancellationToken).ConfigureAwait(false);

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
