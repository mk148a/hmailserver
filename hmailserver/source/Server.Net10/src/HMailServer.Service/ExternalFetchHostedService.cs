using HMailServer.Protocols.Pop3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class ExternalFetchHostedService : BackgroundService
{
    private readonly ExternalFetchHostedServiceOptions _hostedOptions;
    private readonly ExternalFetchProcessorOptions _processorOptions;
    private readonly ExternalFetchProcessor _processor;
    private readonly ILogger<ExternalFetchHostedService> _logger;

    public ExternalFetchHostedService(
        ExternalFetchHostedServiceOptions hostedOptions,
        ExternalFetchProcessorOptions processorOptions,
        ExternalFetchProcessor processor,
        ILogger<ExternalFetchHostedService> logger)
    {
        _hostedOptions = hostedOptions;
        _processorOptions = processorOptions;
        _processor = processor;
        _logger = logger;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(hostedOptions.PollInterval.Ticks, 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                await Task.Delay(_hostedOptions.PollInterval, stoppingToken).ConfigureAwait(false);
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
