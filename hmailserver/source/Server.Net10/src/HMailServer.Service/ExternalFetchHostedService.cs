using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Pop3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class ExternalFetchHostedService : BackgroundService
{
    private readonly ExternalFetchHostedServiceOptions _hostedOptions;
    private readonly ExternalFetchProcessorOptions _processorOptions;
    private readonly ExternalFetchProcessor _processor;
    private readonly IExternalFetchWakeSignal _wakeSignal;
    private readonly ILogger<ExternalFetchHostedService> _logger;
    private readonly ServerReadinessSignal _serverReadinessSignal;

    public ExternalFetchHostedService(
        ExternalFetchHostedServiceOptions hostedOptions,
        ExternalFetchProcessorOptions processorOptions,
        ExternalFetchProcessor processor,
        IExternalFetchWakeSignal wakeSignal,
        ILogger<ExternalFetchHostedService> logger,
        ServerReadinessSignal serverReadinessSignal)
    {
        _hostedOptions = hostedOptions;
        _processorOptions = processorOptions;
        _processor = processor;
        _wakeSignal = wakeSignal;
        _logger = logger;
        _serverReadinessSignal = serverReadinessSignal;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(hostedOptions.PollInterval.Ticks, 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _serverReadinessSignal
            .WaitForBootstrapAsync(stoppingToken)
            .ConfigureAwait(false);

        try
        {
            await _processor.ResetLocksAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External POP3 fetch could not reset stale account locks before polling.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await _processor.RunBatchAsync(_processorOptions, stoppingToken).ConfigureAwait(false);
            var processed = result.MessagesDownloaded +
                result.RemoteMessagesDeleted +
                result.KnownUidsDeleted +
                result.DeferredInactiveAccounts;
            if (processed == 0)
            {
                await _wakeSignal
                    .WaitAsync(_hostedOptions.PollInterval, stoppingToken)
                    .ConfigureAwait(false);
                continue;
            }

            _logger.LogDebug(
                "External POP3 fetch processed {Downloaded} downloads, {Accepted} accepted messages, {Deleted} remote deletes, and {DeletedUids} UID cleanup rows.",
                result.MessagesDownloaded,
                result.MessagesAccepted,
                result.RemoteMessagesDeleted,
                result.KnownUidsDeleted);
        }
    }
}
