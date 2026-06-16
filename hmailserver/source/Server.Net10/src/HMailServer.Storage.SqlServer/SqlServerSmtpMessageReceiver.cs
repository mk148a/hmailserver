using HMailServer.Core.Abstractions;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSmtpMessageReceiver : ISmtpMessageReceiver
{
    public const string InsertQueuedMessageSql = SqlServerSmtpQueueWriter.InsertQueuedMessageSql;

    public const string InsertRecipientSql = SqlServerSmtpQueueWriter.InsertRecipientSql;

    public const string UnlockQueuedMessageSql = SqlServerSmtpQueueWriter.UnlockQueuedMessageSql;

    private readonly SqlServerSmtpQueueWriter _queueWriter;
    private readonly ISmtpRuleProcessor? _ruleProcessor;

    public SqlServerSmtpMessageReceiver(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver,
        ISmtpRuleProcessor? ruleProcessor = null,
        SqlServerSmtpQueueWriter? queueWriter = null)
    {
        _queueWriter = queueWriter ?? new SqlServerSmtpQueueWriter(connectionFactory, pathResolver);
        _ruleProcessor = ruleProcessor;
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
                        ruleBindAddress),
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
}
