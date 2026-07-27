using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class ServerBootstrapper : BackgroundService
{
    private readonly SqlServerFullTextSearchHealthCheck _fullTextSearchHealthCheck;
    private readonly IMessageSearchIndex _messageSearchIndex;
    private readonly ServerReadinessSignal _serverReadinessSignal;
    private readonly ILogger<ServerBootstrapper> _logger;

    public ServerBootstrapper(
        SqlServerFullTextSearchHealthCheck fullTextSearchHealthCheck,
        IMessageSearchIndex messageSearchIndex,
        ServerReadinessSignal serverReadinessSignal,
        ILogger<ServerBootstrapper> logger)
    {
        _fullTextSearchHealthCheck = fullTextSearchHealthCheck;
        _messageSearchIndex = messageSearchIndex;
        _serverReadinessSignal = serverReadinessSignal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var fullTextReady = await _fullTextSearchHealthCheck
                .IsFullTextInstalledAsync(stoppingToken)
                .ConfigureAwait(false);

            if (!fullTextReady)
            {
                throw new InvalidOperationException("SQL Server Full-Text Search is required for hMailServer .NET 10 fast mode.");
            }

            var searchIndexReady = await _messageSearchIndex
                .IsReadyAsync(stoppingToken)
                .ConfigureAwait(false);

            if (!searchIndexReady)
            {
                throw new InvalidOperationException("The hMailServer message search Full-Text index is not ready. Apply Upgrade5708to6000MSSQL.sql first.");
            }

            _serverReadinessSignal.SetReady();
            _logger.LogInformation("hMailServer .NET 10 bootstrap completed. IMAP session engine and background indexer are ready.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _serverReadinessSignal.SetCanceled(stoppingToken);
            throw;
        }
        catch (Exception exception)
        {
            _serverReadinessSignal.SetFailure(exception);
            throw;
        }
    }
}
