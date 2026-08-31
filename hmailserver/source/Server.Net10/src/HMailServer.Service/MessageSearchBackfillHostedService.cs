using HMailServer.Indexing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class MessageSearchBackfillHostedService : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    private readonly MessageSearchBackfillOptions _options;
    private readonly MessageSearchBackfillProcessor _processor;
    private readonly ServerReadinessSignal _serverReadinessSignal;
    private readonly ILogger<MessageSearchBackfillHostedService> _logger;

    public MessageSearchBackfillHostedService(
        MessageSearchBackfillOptions options,
        MessageSearchBackfillProcessor processor,
        ILogger<MessageSearchBackfillHostedService> logger,
        ServerReadinessSignal serverReadinessSignal)
    {
        _options = options;
        _processor = processor;
        _logger = logger;
        _serverReadinessSignal = serverReadinessSignal;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _serverReadinessSignal
            .WaitForBootstrapAsync(stoppingToken)
            .ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                processed = await _processor.RunBatchAsync(_options, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Message search backfill batch failed; retrying after {RetryDelay}.",
                    IdleDelay);
                await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (processed == 0)
            {
                await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug("Indexed {MessageCount} queued messages for SQL Server Full-Text Search.", processed);
            }
        }
    }
}
